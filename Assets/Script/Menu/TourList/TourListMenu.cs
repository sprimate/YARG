using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.EventSystems;
using YARG.Core.Audio;
using YARG.Core.Input;
using YARG.Core.Song;
using YARG.Menu;
using YARG.Menu.ListMenu;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Settings;
using YARG.Song;

namespace YARG
{
    public class TourListMenu : ListMenu<TourListViewType, TourView>
    {
        protected override int ExtraListViewPadding => 15;

        private void OnEnable()
        {
            RequestViewListUpdate();

            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Up",
                    ctx => {
                        SetWrapAroundState(!ctx.IsRepeat);
                        SelectedIndex--;
                    }),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Down",
                    ctx => {
                        SetWrapAroundState(!ctx.IsRepeat);
                        SelectedIndex++;
                    }),
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm",
                    () => {
                        if (CurrentSelection != null)
                        {
                            CurrentSelection.OpenTour();
                        }
                    }),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back",
                    () => MenuManager.Instance.PopMenu()),
                new NavigationScheme.Entry(MenuAction.Yellow, "Menu.Common.EditTour",
                    () => {
                        if (CurrentSelection != null)
                        {
                            EditTour(CurrentSelection.TourData);
                        }
                    }),
                new NavigationScheme.Entry(MenuAction.Blue, "Menu.Common.CreateTour", CreateAndEditTour),
                new NavigationScheme.Entry(MenuAction.Orange, "Menu.Common.Delete", () => DeleteTour().Forget()),

            }, false));
        }

        private void OnDisable()
        {
            // If the editor is still open when this menu goes away, tear it down
            // so we don't leave an extra scheme on the stack or the EventSystem disabled.
            if (_tourToEdit != null)
            {
                CloseEditor();
            }

            Navigator.Instance.PopScheme();
        }

        protected override List<TourListViewType> CreateViewList()
        {
            List<TourListViewType> viewList = new List<TourListViewType>();
            foreach (var tourData in TourManager.GetAllTours())
            {
                viewList.Add(new TourListViewType(tourData));
            }

            Debug.Log($"Added {viewList.Count} tours to the tour list menu");
            return viewList.OrderBy(v => v.TourData.TourName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        protected async UniTask DeleteTour()
        {
            if (CurrentSelection == null)
            {
                return;
            }

            var tour = CurrentSelection.TourData;
            string path = FindExistingTourPath(tour.TourId);
            if (path == null)
            {
                Debug.LogWarning($"Cannot delete tour '{tour.TourName}': no file found for it.");
                return;
            }

            bool confirmed = false;
            var dialog = DialogManager.Instance.ShowConfirmDeleteDialog(
                "Deleting this tour is permanent and cannot be undone.", () =>
                {
                    confirmed = true;
                }, tour.TourName);

            await dialog.WaitUntilClosed();

            if (!confirmed)
            {
                return;
            }

            try
            {
                File.Delete(path);
                Debug.Log($"Deleted tour '{tour.TourName}' at {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to delete tour '{tour.TourName}' at {path}: {ex.Message}");
                return;
            }

            RequestViewListUpdate();
        }

        protected void CreateAndEditTour()
        {
            // Create a new tour and open it in the editor. It is only written
            // to disk when the user saves.
            TourData createdTour = new TourData
            {
                TourId = Guid.NewGuid(),
                TourName = "New Tour",
                Author = "Unknown",
                Shows = new TourShowData[]
                {
                    new TourShowData
                    {
                        ShowName = "Show 1",
                        Songs = Array.Empty<TourSongEntry>(),
                        UnlockConditions = Array.Empty<TourUnlockCondition>(),
                    }
                }
            };

            EditTour(createdTour);
        }

        // =====================================================================
        //  Tour Editor (ALPHA) — OnGUI based, mouse & keyboard only
        // =====================================================================

        private TourData _tourToEdit;

        private Vector2 _mainScroll;
        private Vector2 _pickerScroll;

        // Index of the show currently picking a song, or -1 if the picker is closed.
        private int _pickerShowIndex = -1;
        private string _pickerFilter = "";

        private Texture2D _backgroundTexture;
        private Texture2D _panelTexture;
        private Texture2D _rowEvenTexture;
        private Texture2D _rowOddTexture;

        private EventSystem _disabledEventSystem;

        // Song preview playback for the picker. Only one preview can ever exist
        // at a time — starting a new one always stops the previous one first.
        private PreviewContext _previewContext;
        private CancellationTokenSource _previewCanceller;
        private SongEntry _previewingSong;

        // Buffers so int fields can be typed into freely (e.g. cleared) without snapping.
        private readonly Dictionary<TourUnlockCondition, string> _amountBuffers = new();

        private const float REFERENCE_HEIGHT = 1080f;
        private const float BOTTOM_BAR_HEIGHT = 70f;
        private const int BASE_FONT_SIZE = 20;

        // Uniform height for every control inside a row. Tall enough for the
        // base font size so text is never clipped vertically.
        private const float ROW_HEIGHT = 34f;

        // Keep the editable content in a narrower centered column so controls
        // stay close to the things they affect.
        private const float MAX_CONTENT_WIDTH = 1200f;

        void EditTour(TourData tourData)
        {
            if (tourData == null)
            {
                throw new InvalidOperationException("Tried to open the tour editor with no tour data.");
            }

            // Make sure nothing is null so the editor can assume valid arrays.
            if (tourData.TourId == Guid.Empty)
            {
                tourData.TourId = Guid.NewGuid();
            }
            tourData.Shows ??= Array.Empty<TourShowData>();
            foreach (var show in tourData.Shows)
            {
                show.Songs ??= Array.Empty<TourSongEntry>();
                show.UnlockConditions ??= Array.Empty<TourUnlockCondition>();
            }

            _tourToEdit = tourData;
            _pickerShowIndex = -1;
            _pickerFilter = "";
            _mainScroll = Vector2.zero;
            _amountBuffers.Clear();
            _libraryLookupCache.Clear();

            // Block all input to the canvases / UI underneath the editor.
            if (EventSystem.current != null)
            {
                _disabledEventSystem = EventSystem.current;
                _disabledEventSystem.enabled = false;
            }

            // Editor navigation scheme: Green = save, Red = back. Nothing else.
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm",
                    () => SaveEditor()),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back",
                    () => {
                        if (_pickerShowIndex >= 0)
                        {
                            ClosePicker();
                        }
                        else
                        {
                            CloseEditor();
                        }
                    }),
            }, false));
        }

        private void CloseEditor()
        {
            if (_tourToEdit == null)
            {
                return;
            }

            StopPreview();

            _tourToEdit = null;
            _pickerShowIndex = -1;

            Navigator.Instance?.PopScheme();

            if (_disabledEventSystem != null)
            {
                _disabledEventSystem.enabled = true;
                _disabledEventSystem = null;
            }
        }

        private void SaveEditor()
        {
            if (_tourToEdit == null)
            {
                return;
            }

            var tour = _tourToEdit;

            string path = FindExistingTourPath(tour.TourId);
            if (path == null)
            {
                // New tour: save into the top tour (song folder) location.
                var folders = SettingsManager.Settings.SongFolders;
                if (folders == null || folders.Count == 0)
                {
                    Debug.LogError("Cannot save tour: no song folders are configured. " +
                        "Add a song folder in the settings first.");
                    return;
                }

                string fileName = SanitizeFileName(tour.TourName);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = tour.TourId.ToString();
                }

                path = Path.Combine(folders[0], fileName + ".tour");

                // Don't clobber an unrelated file with the same name.
                int suffix = 1;
                while (File.Exists(path))
                {
                    path = Path.Combine(folders[0], $"{fileName}_{suffix}.tour");
                    suffix++;
                }
            }

            string json = JsonConvert.SerializeObject(tour, Formatting.Indented);
            File.WriteAllText(path, json);
            Debug.Log($"Saved tour '{tour.TourName}' to {path}");

            CloseEditor();

            // Reload the list so the saved tour shows up.
            RequestViewListUpdate();
        }

        private static string FindExistingTourPath(Guid tourId)
        {
            foreach (var dir in SettingsManager.Settings.SongFolders)
            {
                foreach (var file in Directory.GetFiles(dir, "*.tour", SearchOption.AllDirectories))
                {
                    try
                    {
                        var data = JsonConvert.DeserializeObject<TourData>(File.ReadAllText(file));
                        if (data != null && data.TourId == tourId)
                        {
                            return file;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Skipping unreadable tour file '{file}': {ex.Message}");
                    }
                }
            }

            return null;
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "";
            }

            var invalid = Path.GetInvalidFileNameChars();
            return new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        }

        private void ClosePicker()
        {
            _pickerShowIndex = -1;
            StopPreview();
        }

        // Cache of "is this artist/song in the local library" checks so we don't
        // do a library lookup per row per GUI event while typing.
        private readonly Dictionary<(string, string), bool> _libraryLookupCache = new();

        private bool IsSongInLibrary(string artist, string songName)
        {
            artist ??= "";
            songName ??= "";

            var key = (artist, songName);
            if (_libraryLookupCache.TryGetValue(key, out bool found))
            {
                return found;
            }

            // Same match logic as TourShowData.GetSongEntries/SongContainer.GetSongEntry,
            // but without the error logging on a miss (a miss is a valid state here).
            found = false;
            if (SongContainer.Artists.TryGetValue(new SortString(artist), out var artistSongs))
            {
                found = artistSongs.Any(songEntry => songEntry?.Name == songName);
            }

            _libraryLookupCache[key] = found;
            return found;
        }

        // ─── Song preview playback ────────────────────────────────────────

        private async void PlayPreview(SongEntry entry)
        {
            // Always tear down the current preview first so two songs can
            // never play at once.
            StopPreview();

            float previewVolume = SettingsManager.Settings.PreviewVolume.Value;
            if (previewVolume == 0)
            {
                Debug.LogWarning("Cannot play song preview: preview volume is set to 0.");
                return;
            }

            const double FADE_DURATION = 1.25;
            var canceller = new CancellationTokenSource();
            _previewCanceller = canceller;
            _previewingSong = entry;

            var context = await PreviewContext.Create(entry, previewVolume,
                GlobalVariables.State.SongSpeed, 0, FADE_DURATION, canceller);

            if (context == null)
            {
                if (_previewingSong == entry)
                {
                    _previewingSong = null;
                }
                return;
            }

            // A newer preview/stop request happened while this one was loading.
            if (canceller.IsCancellationRequested || _previewCanceller != canceller)
            {
                context.Dispose();
                return;
            }

            _previewContext = context;
        }

        private void StopPreview()
        {
            _previewCanceller?.Cancel();
            _previewCanceller = null;
            _previewContext?.Dispose();
            _previewContext = null;
            _previewingSong = null;
        }

        // ─── Array helpers (TourData uses arrays, so resize on edit) ──────────

        private static T[] ArrayAppend<T>(T[] array, T item)
        {
            var result = new T[array.Length + 1];
            Array.Copy(array, result, array.Length);
            result[^1] = item;
            return result;
        }

        private static T[] ArrayRemoveAt<T>(T[] array, int index)
        {
            var result = new T[array.Length - 1];
            Array.Copy(array, 0, result, 0, index);
            Array.Copy(array, index + 1, result, index, array.Length - index - 1);
            return result;
        }

        private static void ArraySwap<T>(T[] array, int a, int b)
        {
            (array[a], array[b]) = (array[b], array[a]);
        }

        // ─── OnGUI rendering ──────────────────────────────────────────────────

        void OnGUI()
        {
            if (_tourToEdit == null)
            {
                return;
            }

            EnsureTextures();

            // Scale the whole GUI so it looks consistent at any resolution.
            float scale = Screen.height / REFERENCE_HEIGHT;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            // Fully opaque background covering everything.
            GUI.DrawTexture(new Rect(0, 0, width, height), _backgroundTexture);

            DrawHeader(width);

            float contentTop = 100f;
            float contentBottom = height - BOTTOM_BAR_HEIGHT;

            if (_pickerShowIndex >= 0 && _pickerShowIndex < _tourToEdit.Shows.Length)
            {
                DrawSongPicker(new Rect(0, contentTop, width, contentBottom - contentTop));
            }
            else
            {
                _pickerShowIndex = -1;
                DrawTourEditor(new Rect(0, contentTop, width, contentBottom - contentTop));
            }

            DrawBottomBar(new Rect(0, contentBottom, width, BOTTOM_BAR_HEIGHT));
        }

        private void EnsureTextures()
        {
            if (_backgroundTexture == null)
            {
                _backgroundTexture = new Texture2D(1, 1);
                _backgroundTexture.SetPixel(0, 0, new Color(0.10f, 0.10f, 0.13f, 1f));
                _backgroundTexture.Apply();
            }

            if (_panelTexture == null)
            {
                _panelTexture = new Texture2D(1, 1);
                _panelTexture.SetPixel(0, 0, new Color(0.16f, 0.16f, 0.21f, 1f));
                _panelTexture.Apply();
            }

            if (_rowEvenTexture == null)
            {
                _rowEvenTexture = new Texture2D(1, 1);
                _rowEvenTexture.SetPixel(0, 0, new Color(0.22f, 0.22f, 0.28f, 1f));
                _rowEvenTexture.Apply();
            }

            if (_rowOddTexture == null)
            {
                _rowOddTexture = new Texture2D(1, 1);
                _rowOddTexture.SetPixel(0, 0, new Color(0.13f, 0.13f, 0.17f, 1f));
                _rowOddTexture.Apply();
            }
        }

        private void DrawHeader(float width)
        {
            GUI.Label(new Rect(0, 8, width, 44), "TOUR EDITOR", TitleStyle());
            GUI.Label(new Rect(0, 54, width, 30),
                "ALPHA — This editor is mouse and keyboard only.", WarningStyle());
        }

        private static Rect CenteredColumn(Rect area, float maxWidth)
        {
            float colWidth = Mathf.Min(area.width - 80f, maxWidth);
            float colX = area.x + (area.width - colWidth) / 2f;
            return new Rect(colX, area.y, colWidth, area.height);
        }

        private void DrawTourEditor(Rect area)
        {
            var tour = _tourToEdit;

            GUILayout.BeginArea(CenteredColumn(area, MAX_CONTENT_WIDTH));
            _mainScroll = GUILayout.BeginScrollView(_mainScroll);

            // ── Tour info ────────────────────────────────────────────────────
            GUILayout.Label("Tour Info", SectionHeaderStyle());
            GUILayout.BeginVertical(PanelStyle());

            GUILayout.BeginHorizontal();
            GUILayout.Label("Tour Name", LabelStyle(), GUILayout.Width(160),
                GUILayout.Height(ROW_HEIGHT));
            tour.TourName = GUILayout.TextField(tour.TourName ?? "", TextFieldStyle(),
                GUILayout.MinWidth(400));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Author", LabelStyle(), GUILayout.Width(160),
                GUILayout.Height(ROW_HEIGHT));
            tour.Author = GUILayout.TextField(tour.Author ?? "", TextFieldStyle(),
                GUILayout.MinWidth(400));
            GUILayout.EndHorizontal();

            tour.ShowAllLockedShows = GUILayout.Toggle(tour.ShowAllLockedShows,
                " Show all locked shows", ToggleStyle(), GUILayout.Height(ROW_HEIGHT));

            GUILayout.EndVertical();

            GUILayout.Space(16);

            // ── Shows ────────────────────────────────────────────────────────
            GUILayout.BeginHorizontal();
            GUILayout.Label("Shows", SectionHeaderStyle(), GUILayout.ExpandWidth(false));
            GUILayout.Space(12);
            if (GUILayout.Button("+ Add Show", ButtonStyle(), GUILayout.Width(160),
                GUILayout.Height(34)))
            {
                tour.Shows = ArrayAppend(tour.Shows, new TourShowData
                {
                    ShowName = $"Show {tour.Shows.Length + 1}",
                    Songs = Array.Empty<TourSongEntry>(),
                    UnlockConditions = Array.Empty<TourUnlockCondition>(),
                });
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            for (int i = 0; i < tour.Shows.Length; i++)
            {
                if (DrawShow(tour, i))
                {
                    // Show was removed; indices shifted, bail out of the loop this frame.
                    break;
                }
                GUILayout.Space(10);
            }

            GUILayout.Space(30);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <returns>True if the show at <paramref name="showIndex"/> was removed.</returns>
        private bool DrawShow(TourData tour, int showIndex)
        {
            var show = tour.Shows[showIndex];
            show.Songs ??= Array.Empty<TourSongEntry>();
            show.UnlockConditions ??= Array.Empty<TourUnlockCondition>();

            GUILayout.BeginVertical(PanelStyle());

            // Show header row: delete, reorder, then the name — controls sit
            // right next to what they act on.
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("X", ButtonStyle(), GUILayout.Width(36),
                GUILayout.Height(ROW_HEIGHT)))
            {
                tour.Shows = ArrayRemoveAt(tour.Shows, showIndex);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                return true;
            }

            GUI.enabled = showIndex > 0;
            if (GUILayout.Button("▲", ButtonStyle(), GUILayout.Width(36),
                GUILayout.Height(ROW_HEIGHT)))
            {
                ArraySwap(tour.Shows, showIndex, showIndex - 1);
            }
            GUI.enabled = showIndex < tour.Shows.Length - 1;
            if (GUILayout.Button("▼", ButtonStyle(), GUILayout.Width(36),
                GUILayout.Height(ROW_HEIGHT)))
            {
                ArraySwap(tour.Shows, showIndex, showIndex + 1);
            }
            GUI.enabled = true;

            GUILayout.Space(8);
            GUILayout.Label($"Show {showIndex + 1}", LabelStyle(), GUILayout.Width(100),
                GUILayout.Height(ROW_HEIGHT));
            show.ShowName = GUILayout.TextField(show.ShowName ?? "", TextFieldStyle(),
                GUILayout.MinWidth(350));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            show.ShowLockedSongs = GUILayout.Toggle(show.ShowLockedSongs,
                " Show locked songs", ToggleStyle(), GUILayout.Height(ROW_HEIGHT));

            GUILayout.Space(6);

            // ── Songs ────────────────────────────────────────────────────────
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Songs ({show.Songs.Length})", BoldLabelStyle(),
                GUILayout.ExpandWidth(false), GUILayout.Height(ROW_HEIGHT));
            GUILayout.Space(12);
            if (GUILayout.Button("+ Add From Library", ButtonStyle(), GUILayout.Width(230),
                GUILayout.Height(ROW_HEIGHT)))
            {
                _pickerShowIndex = showIndex;
                _pickerFilter = "";
                _pickerScroll = Vector2.zero;
            }
            GUILayout.Space(8);
            if (GUILayout.Button("+ Custom Song", ButtonStyle(), GUILayout.Width(190),
                GUILayout.Height(ROW_HEIGHT)))
            {
                // Blank entry the user can type into — for songs that aren't
                // in the local library (yet).
                show.Songs = ArrayAppend(show.Songs, new TourSongEntry
                {
                    Artist = "",
                    SongName = "",
                });
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            for (int s = 0; s < show.Songs.Length; s++)
            {
                var song = show.Songs[s];

                // Alternating row backgrounds so each row's buttons are easy to match up.
                GUILayout.BeginHorizontal(RowStyle(s));

                if (GUILayout.Button("X", ButtonStyle(), GUILayout.Width(36),
                    GUILayout.Height(ROW_HEIGHT)))
                {
                    show.Songs = ArrayRemoveAt(show.Songs, s);
                    GUILayout.EndHorizontal();
                    break;
                }

                GUI.enabled = s > 0;
                if (GUILayout.Button("▲", ButtonStyle(), GUILayout.Width(36),
                    GUILayout.Height(ROW_HEIGHT)))
                {
                    ArraySwap(show.Songs, s, s - 1);
                }
                GUI.enabled = s < show.Songs.Length - 1;
                if (GUILayout.Button("▼", ButtonStyle(), GUILayout.Width(36),
                    GUILayout.Height(ROW_HEIGHT)))
                {
                    ArraySwap(show.Songs, s, s + 1);
                }
                GUI.enabled = true;

                GUILayout.Space(8);
                GUILayout.Label($"{s + 1}.", LabelStyle(), GUILayout.Width(40),
                    GUILayout.Height(ROW_HEIGHT));

                // Editable fields — picker-added songs come pre-populated, but
                // anything can be typed in manually (including songs that
                // aren't in the local library).
                GUILayout.Label("Artist", LabelStyle(), GUILayout.Width(70),
                    GUILayout.Height(ROW_HEIGHT));
                song.Artist = GUILayout.TextField(song.Artist ?? "", TextFieldStyle(),
                    GUILayout.Width(300));
                GUILayout.Space(8);
                GUILayout.Label("Song", LabelStyle(), GUILayout.Width(65),
                    GUILayout.Height(ROW_HEIGHT));
                song.SongName = GUILayout.TextField(song.SongName ?? "", TextFieldStyle(),
                    GUILayout.MinWidth(300));

                // Flag songs the local library can't resolve so typos are obvious.
                if (!IsSongInLibrary(song.Artist, song.SongName))
                {
                    GUILayout.Space(8);
                    GUILayout.Label("⚠ not in library", MissingLabelStyle(),
                        GUILayout.ExpandWidth(false), GUILayout.Height(ROW_HEIGHT));
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);

            // ── Unlock conditions ────────────────────────────────────────────
            GUILayout.BeginHorizontal();
            GUILayout.Label("Unlock Conditions", BoldLabelStyle(),
                GUILayout.ExpandWidth(false), GUILayout.Height(ROW_HEIGHT));
            GUILayout.Space(12);
            if (GUILayout.Button("+ Add Condition", ButtonStyle(), GUILayout.Width(200),
                GUILayout.Height(ROW_HEIGHT)))
            {
                show.UnlockConditions = ArrayAppend(show.UnlockConditions,
                    new TourUnlockCondition());
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (show.UnlockConditions.Length == 0)
            {
                GUILayout.Label("(none — show is always unlocked)", LabelStyle(),
                    GUILayout.Height(ROW_HEIGHT));
            }

            for (int c = 0; c < show.UnlockConditions.Length; c++)
            {
                var condition = show.UnlockConditions[c];

                GUILayout.BeginHorizontal(RowStyle(c));

                if (GUILayout.Button("X", ButtonStyle(), GUILayout.Width(36),
                    GUILayout.Height(ROW_HEIGHT)))
                {
                    _amountBuffers.Remove(condition);
                    show.UnlockConditions = ArrayRemoveAt(show.UnlockConditions, c);
                    GUILayout.EndHorizontal();
                    break;
                }

                GUILayout.Space(8);
                var conditionNames = Enum.GetNames(typeof(TourUnlockCondition.ConditionType));
                int selected = (int) condition.TypeOfCondition;
                int newSelected = GUILayout.Toolbar(selected, conditionNames, ButtonStyle(),
                    GUILayout.Width(500), GUILayout.Height(ROW_HEIGHT));
                if (newSelected != selected)
                {
                    condition.TypeOfCondition = (TourUnlockCondition.ConditionType) newSelected;
                }

                GUILayout.Space(12);
                GUILayout.Label("Required", LabelStyle(), GUILayout.Width(90),
                    GUILayout.Height(ROW_HEIGHT));

                if (!_amountBuffers.TryGetValue(condition, out string buffer))
                {
                    buffer = condition.RequiredAmount.ToString();
                }
                string newBuffer = GUILayout.TextField(buffer, TextFieldStyle(),
                    GUILayout.Width(120));
                _amountBuffers[condition] = newBuffer;
                if (int.TryParse(newBuffer, out int amount))
                {
                    condition.RequiredAmount = amount;
                }
                else if (string.IsNullOrEmpty(newBuffer))
                {
                    condition.RequiredAmount = 0;
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            return false;
        }

        private void DrawSongPicker(Rect area)
        {
            var show = _tourToEdit.Shows[_pickerShowIndex];

            GUILayout.BeginArea(CenteredColumn(area, MAX_CONTENT_WIDTH));
            GUILayout.BeginVertical(PanelStyle(), GUILayout.ExpandHeight(true));

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Add Songs to \"{show.ShowName}\"", SectionHeaderStyle(),
                GUILayout.ExpandWidth(false));
            GUILayout.Space(12);
            if (GUILayout.Button("Done", ButtonStyle(), GUILayout.Width(120),
                GUILayout.Height(34)))
            {
                ClosePicker();
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.EndArea();
                return;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // Filter box
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter", LabelStyle(), GUILayout.Width(70),
                GUILayout.Height(ROW_HEIGHT));
            _pickerFilter = GUILayout.TextField(_pickerFilter, TextFieldStyle(),
                GUILayout.MinWidth(400));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Table header (buttons column first, then Artist / Song Name)
            GUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(210));
            GUILayout.Space(8);
            GUILayout.Label("Artist", BoldLabelStyle(), GUILayout.Width(400),
                GUILayout.Height(ROW_HEIGHT));
            GUILayout.Label("Song Name", BoldLabelStyle(), GUILayout.ExpandWidth(true),
                GUILayout.Height(ROW_HEIGHT));
            GUILayout.EndHorizontal();

            var allSongs = SongContainer.Songs;
            const int MAX_ROWS = 250;
            int shown = 0;
            bool truncated = false;

            _pickerScroll = GUILayout.BeginScrollView(_pickerScroll);
            foreach (var entry in allSongs)
            {
                string artist = entry.Artist.Original;
                string name = entry.Name.Original;

                if (!string.IsNullOrEmpty(_pickerFilter) &&
                    artist.IndexOf(_pickerFilter, StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf(_pickerFilter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (shown >= MAX_ROWS)
                {
                    truncated = true;
                    break;
                }

                GUILayout.BeginHorizontal(RowStyle(shown));
                shown++;

                bool alreadyAdded = show.Songs.Any(s =>
                    s.Artist == artist && s.SongName == name);
                GUI.enabled = !alreadyAdded;
                if (GUILayout.Button(alreadyAdded ? "Added" : "+ Add", ButtonStyle(),
                    GUILayout.Width(90), GUILayout.Height(ROW_HEIGHT)))
                {
                    show.Songs = ArrayAppend(show.Songs, new TourSongEntry
                    {
                        Artist = artist,
                        SongName = name,
                    });
                }
                GUI.enabled = true;

                bool isPreviewing = _previewingSong == entry;
                if (GUILayout.Button(isPreviewing ? "■ Stop" : "▶ Preview", ButtonStyle(),
                    GUILayout.Width(120), GUILayout.Height(ROW_HEIGHT)))
                {
                    if (isPreviewing)
                    {
                        StopPreview();
                    }
                    else
                    {
                        PlayPreview(entry);
                    }
                }

                GUILayout.Space(8);
                GUILayout.Label(artist, LabelStyle(), GUILayout.Width(400),
                    GUILayout.Height(ROW_HEIGHT));
                GUILayout.Label(name, LabelStyle(), GUILayout.ExpandWidth(true),
                    GUILayout.Height(ROW_HEIGHT));
                GUILayout.EndHorizontal();
            }

            if (truncated)
            {
                GUILayout.Label($"…more results hidden, refine the filter (showing first {MAX_ROWS}).",
                    LabelStyle(), GUILayout.Height(ROW_HEIGHT));
            }

            if (shown == 0)
            {
                GUILayout.Label("No songs match the filter.", LabelStyle(),
                    GUILayout.Height(ROW_HEIGHT));
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawBottomBar(Rect area)
        {
            GUI.DrawTexture(area, _panelTexture);

            GUILayout.BeginArea(area);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();

            var oldColor = GUI.backgroundColor;

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.3f);
            if (GUILayout.Button("● Confirm — Save Tour", ButtonStyle(), GUILayout.Height(40),
                GUILayout.Width(300)))
            {
                SaveEditor();
            }

            GUILayout.Space(30);

            GUI.backgroundColor = new Color(0.9f, 0.25f, 0.25f);
            string redLabel = _pickerShowIndex >= 0
                ? "● Back — Close Song List"
                : "● Back — Discard Changes";
            if (GUILayout.Button(redLabel, ButtonStyle(), GUILayout.Height(40),
                GUILayout.Width(300)))
            {
                if (_pickerShowIndex >= 0)
                {
                    ClosePicker();
                }
                else
                {
                    CloseEditor();
                }
            }

            GUI.backgroundColor = oldColor;

            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ─── Styles ───────────────────────────────────────────────────────────

        private GUIStyle _titleStyle;
        private GUIStyle _warningStyle;
        private GUIStyle _sectionHeaderStyle;
        private GUIStyle _boldLabelStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _missingLabelStyle;
        private GUIStyle _toggleStyle;
        private GUIStyle _textFieldStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _rowEvenStyle;
        private GUIStyle _rowOddStyle;

        private GUIStyle LabelStyle()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = BASE_FONT_SIZE,
                    alignment = TextAnchor.MiddleLeft,
                    // Never visually cut text off, even if the layout rect ends
                    // up shorter than the rendered glyphs.
                    clipping = TextClipping.Overflow,
                };
            }
            return _labelStyle;
        }

        private GUIStyle MissingLabelStyle()
        {
            if (_missingLabelStyle == null)
            {
                _missingLabelStyle = new GUIStyle(LabelStyle());
                _missingLabelStyle.normal.textColor = new Color(1f, 0.75f, 0.25f);
            }
            return _missingLabelStyle;
        }

        private GUIStyle ToggleStyle()
        {
            if (_toggleStyle == null)
            {
                _toggleStyle = new GUIStyle(GUI.skin.toggle)
                {
                    fontSize = BASE_FONT_SIZE,
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Overflow,
                };
            }
            return _toggleStyle;
        }

        private GUIStyle TextFieldStyle()
        {
            if (_textFieldStyle == null)
            {
                _textFieldStyle = new GUIStyle(GUI.skin.textField)
                {
                    fontSize = BASE_FONT_SIZE,
                    alignment = TextAnchor.MiddleLeft,
                    fixedHeight = ROW_HEIGHT,
                };
            }
            return _textFieldStyle;
        }

        private GUIStyle ButtonStyle()
        {
            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = BASE_FONT_SIZE,
                    alignment = TextAnchor.MiddleCenter,
                };
            }
            return _buttonStyle;
        }

        private GUIStyle TitleStyle()
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 36,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
                _titleStyle.normal.textColor = Color.white;
            }
            return _titleStyle;
        }

        private GUIStyle WarningStyle()
        {
            if (_warningStyle == null)
            {
                _warningStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    alignment = TextAnchor.MiddleCenter,
                };
                _warningStyle.normal.textColor = new Color(1f, 0.75f, 0.25f);
            }
            return _warningStyle;
        }

        private GUIStyle SectionHeaderStyle()
        {
            if (_sectionHeaderStyle == null)
            {
                _sectionHeaderStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 26,
                    fontStyle = FontStyle.Bold,
                };
                _sectionHeaderStyle.normal.textColor = Color.white;
            }
            return _sectionHeaderStyle;
        }

        private GUIStyle BoldLabelStyle()
        {
            if (_boldLabelStyle == null)
            {
                _boldLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = BASE_FONT_SIZE + 1,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Overflow,
                };
            }
            return _boldLabelStyle;
        }

        private GUIStyle RowStyle(int index)
        {
            if (_rowEvenStyle == null)
            {
                _rowEvenStyle = new GUIStyle
                {
                    padding = new RectOffset(8, 8, 6, 6),
                    margin = new RectOffset(0, 0, 2, 2),
                };
                _rowEvenStyle.normal.background = _rowEvenTexture;

                _rowOddStyle = new GUIStyle(_rowEvenStyle);
                _rowOddStyle.normal.background = _rowOddTexture;
            }
            return index % 2 == 0 ? _rowEvenStyle : _rowOddStyle;
        }

        private GUIStyle PanelStyle()
        {
            if (_panelStyle == null)
            {
                _panelStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(12, 12, 10, 10),
                };
                _panelStyle.normal.background = _panelTexture;
            }
            return _panelStyle;
        }
    }
}
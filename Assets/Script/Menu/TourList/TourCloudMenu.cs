using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using YARG.Menu.Navigation;
using YARG.Core.Input;
using YARG.Song;

namespace YARG
{
    // (sprimate) Follows the TourEditorMenu paradigm: a fully code-driven menu
    // with no scene changes. Unlike the editor this one uses UI Toolkit — the
    // host GameObject, PanelSettings and every VisualElement are created at
    // runtime and cached for reuse.
    public static class TourCloudMenu
    {
        // =====================================================================
        //  Cloud Tours (ALPHA) — UI Toolkit based, mouse & keyboard only
        // =====================================================================

        public static Action RequestViewListUpdate;

        private enum Tab
        {
            Browse,
            MyTours,
        }

        private enum SortColumn
        {
            None,
            Name,
            Author,
            Rating,
            Downloads,
            Downloaded,
        }

        // ─── Cached runtime objects (created once, reused forever) ───────────

        private static GameObject _host;
        private static UIDocument _document;
        private static PanelSettings _panelSettings;

        // ─── Cached visual elements ──────────────────────────────────────────

        private static VisualElement _root;
        private static Label _statusLabel;
        private static Button _browseTabButton;
        private static Button _myToursTabButton;
        private static VisualElement _browseContent;
        private static VisualElement _myToursContent;
        private static TextField _searchField;
        private static ScrollView _browseList;
        private static ScrollView _myToursList;
        private static VisualElement _modalOverlay;
        private static Button _backBarButton;

        // Invisible full-screen element that sits on top of everything and
        // swallows all pointer events until the mouse button that opened the
        // menu is released. The menu is opened from a uGUI button's
        // pointer-DOWN handler, and UI Toolkit processes that same frame's
        // input afterwards — without this, the still-held press registers as
        // a PointerDown on whatever now sits under the cursor (e.g. the Back
        // button), and releasing completes it as a click.
        private static VisualElement _inputShield;
        private static readonly Dictionary<SortColumn, Button> _headerButtons = new();

        // ─── State ───────────────────────────────────────────────────────────

        private static bool _isOpen;

        // Incremented every Open(). Async work captures the generation it
        // started under and bails out if the menu was closed/reopened in the
        // meantime, so stale continuations can never touch a rebuilt (or
        // destroyed) element tree.
        private static int _openGeneration;
        private static Tab _activeTab = Tab.Browse;
        private static SortColumn _sortColumn = SortColumn.None;
        private static bool _sortAscending = true;
        private static string _searchText = "";

        private static List<CloudTourMetadata> _cloudTours = new();
        private static List<TourData> _localTours = new();
        private static readonly HashSet<string> _localTourIds = new(StringComparer.OrdinalIgnoreCase);
        private static bool _busy;

        private static UniTaskCompletionSource<bool> _modalCompletion;

        // The EventSystem we disabled while the menu is open, so the UI
        // underneath cannot be clicked or navigated (same trick as
        // TourEditorMenu). UI Toolkit keeps working through its own built-in
        // runtime event system, just like IMGUI does for the editor.
        private static EventSystem _disabledEventSystem;

        // ─── Style constants ─────────────────────────────────────────────────

        // Same fully opaque background as TourEditorMenu's _backgroundTexture.
        private static readonly Color BACKGROUND_COLOR = new(0.10f, 0.10f, 0.13f, 1f);
        private static readonly Color PANEL_COLOR = new(0.16f, 0.16f, 0.20f, 1f);
        private static readonly Color ROW_EVEN_COLOR = new(1f, 1f, 1f, 0.03f);
        private static readonly Color ROW_ODD_COLOR = new(1f, 1f, 1f, 0.07f);
        private static readonly Color ACCENT_COLOR = new(0.26f, 0.55f, 0.96f);
        private static readonly Color STAR_COLOR = new(1f, 0.8f, 0.15f);
        private static readonly Color TEXT_COLOR = new(0.92f, 0.92f, 0.95f);
        private static readonly Color MUTED_TEXT_COLOR = new(0.65f, 0.65f, 0.7f);
        private static readonly Color ERROR_COLOR = new(1f, 0.4f, 0.4f);

        private const int SORT_ORDER = 100;

        // ─── Public API ──────────────────────────────────────────────────────

        public static void Open()
        {
            if (_isOpen)
            {
                return;
            }

            _isOpen = true;
            _openGeneration++;
            _busy = false;
            _searchText = "";
            _sortColumn = SortColumn.None;
            _sortAscending = true;
            _activeTab = Tab.Browse;

            EnsureHost();

            // The host MUST be activated before building the UI: deactivating
            // a UIDocument's GameObject destroys its rootVisualElement, and it
            // only exists again once the object is re-enabled. Building first
            // would NRE on every open after the first.
            _host.SetActive(true);
            BuildUi();

            // No pointer interaction with the new UI until the press that
            // opened this menu has been released.
            RaiseInputShield();
            LowerInputShieldWhenPointerReleased().Forget();

            // Block all input to the canvases / UI underneath the menu,
            // exactly like TourEditorMenu does.
            if (EventSystem.current != null)
            {
                _disabledEventSystem = EventSystem.current;
                _disabledEventSystem.enabled = false;
            }

            // Red = back. Everything else is mouse-driven UI Toolkit.
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back",
                    () =>
                    {
                        if (_modalOverlay != null)
                        {
                            ResolveModal(false);
                        }
                        else
                        {
                            Close();
                        }
                    }),
            }, false));

            RefreshLocalTours();
            RefreshCloudTours().Forget();
        }

        public static void Close()
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;

            if (_modalOverlay != null)
            {
                ResolveModal(false);
            }

            Navigator.Instance?.PopScheme();

            if (_disabledEventSystem != null)
            {
                _disabledEventSystem.enabled = true;
                _disabledEventSystem = null;
            }

            if (_host != null)
            {
                _host.SetActive(false);
            }

            // Deactivating the host destroys the UIDocument's element tree, so
            // every cached reference is now stale. Clear them so nothing can
            // accidentally write into a dead hierarchy — they are all rebuilt
            // from scratch in BuildUi() on the next Open().
            _root = null;
            _statusLabel = null;
            _browseTabButton = null;
            _myToursTabButton = null;
            _browseContent = null;
            _myToursContent = null;
            _searchField = null;
            _browseList = null;
            _myToursList = null;
            _backBarButton = null;
            _inputShield = null;
            _headerButtons.Clear();
        }

        /// <summary>Called from TourListMenu.OnDisable so the overlay never outlives the menu.</summary>
        public static void OnDisable()
        {
            if (_isOpen)
            {
                Close();
            }
        }

        // ─── Host / panel creation ───────────────────────────────────────────

        private static void EnsureHost()
        {
            if (_host != null)
            {
                return;
            }

            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.name = "TourCloudMenuPanelSettings";
            _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            _panelSettings.sortingOrder = SORT_ORDER;

            // A theme isn't strictly required since every element is styled
            // inline, but use one if any is already loaded.
            var theme = Resources.FindObjectsOfTypeAll<ThemeStyleSheet>().FirstOrDefault();
            if (theme != null)
            {
                _panelSettings.themeStyleSheet = theme;
            }

            _host = new GameObject("TourCloudMenu (Runtime)");
            UnityEngine.Object.DontDestroyOnLoad(_host);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
        }

        // ─── UI construction ─────────────────────────────────────────────────

        private static void BuildUi()
        {
            var docRoot = _document.rootVisualElement;
            docRoot.Clear();
            _headerButtons.Clear();
            _modalOverlay = null;

            // Guarantee text renders even without a theme style sheet.
            docRoot.style.unityFontDefinition = new StyleFontDefinition(
                FontDefinition.FromFont(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")));

            // The document root is NOT stretched automatically when the
            // UIDocument has no source asset — without this it has zero height
            // and everything collapses into a strip at the top of the screen.
            // Anchor it to every edge of the panel so it is always exactly
            // full screen.
            docRoot.style.position = Position.Absolute;
            docRoot.style.left = 0;
            docRoot.style.right = 0;
            docRoot.style.top = 0;
            docRoot.style.bottom = 0;

            // Fully opaque, full-screen box covering everything, exactly like
            // TourEditorMenu's background texture — no bleed-through regardless
            // of content. Picking mode Position means it also swallows every
            // pointer event.
            _root = new VisualElement
            {
                pickingMode = PickingMode.Position,
                style =
                {
                    position = Position.Absolute,
                    left = 0, right = 0, top = 0, bottom = 0,
                    width = Length.Percent(100),
                    height = Length.Percent(100),
                    backgroundColor = BACKGROUND_COLOR,
                    color = TEXT_COLOR,
                    fontSize = 18,
                },
            };
            docRoot.Add(_root);

            // ── Header (mimics TourEditorMenu.DrawHeader) ─────────────────────
            // Pushed down so the dev overlays (FPS/memory) drawn along the top
            // of the screen never overlap the title.
            _root.Add(new Label("CLOUD TOURS")
            {
                style =
                {
                    fontSize = 34, unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginTop = 60,
                },
            });
            _root.Add(new Label("ALPHA — This menu is mouse and keyboard only.")
            {
                style =
                {
                    fontSize = 18,
                    color = new Color(1f, 0.75f, 0.25f),
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginBottom = 10,
                },
            });

            // ── Content column (centered, like the editor's content column) ───
            var panel = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    width = Length.Percent(100),
                    maxWidth = 1500,
                    alignSelf = Align.Center,
                    paddingLeft = 25, paddingRight = 25, paddingTop = 10, paddingBottom = 10,
                },
            };
            _root.Add(panel);

            // Tab bar ----------------------------------------------------------
            var tabRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, marginBottom = 12 },
            };
            panel.Add(tabRow);

            _browseTabButton = MakeButton("BROWSE CLOUD TOURS", () => SwitchTab(Tab.Browse), ACCENT_COLOR);
            _myToursTabButton = MakeButton("MY TOURS", () => SwitchTab(Tab.MyTours), ACCENT_COLOR);
            _browseTabButton.style.marginRight = 10;
            tabRow.Add(_browseTabButton);
            tabRow.Add(_myToursTabButton);

            // Status label -----------------------------------------------------
            _statusLabel = new Label("")
            {
                style = { fontSize = 16, color = MUTED_TEXT_COLOR, marginBottom = 8, minHeight = 20 },
            };
            panel.Add(_statusLabel);

            // Tab contents -----------------------------------------------------
            _browseContent = BuildBrowseTab();
            _myToursContent = BuildMyToursTab();
            panel.Add(_browseContent);
            panel.Add(_myToursContent);

            // ── Bottom bar (mimics TourEditorMenu.DrawBottomBar): shows what
            //    each colored controller/menu button does. ──────────────────────
            _root.Add(BuildBottomBar());

            SwitchTab(_activeTab);
        }

        private static VisualElement BuildBottomBar()
        {
            var bar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center,
                    height = 70,
                    flexShrink = 0,
                    backgroundColor = PANEL_COLOR,
                },
            };

            var backButton = MakeButton("● Back — Close Cloud Tours", () =>
            {
                if (_modalOverlay != null)
                {
                    ResolveModal(false);
                }
                else
                {
                    Close();
                }
            }, new Color(0.9f, 0.25f, 0.25f));
            backButton.style.width = 300;
            backButton.style.height = 40;
            bar.Add(backButton);
            _backBarButton = backButton;

            return bar;
        }

        private static VisualElement BuildBrowseTab()
        {
            var content = new VisualElement { style = { flexGrow = 1 } };

            // Search + refresh row
            var searchRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 8 },
            };
            content.Add(searchRow);

            searchRow.Add(new Label("Search:")
            {
                style = { marginRight = 8, color = MUTED_TEXT_COLOR },
            });

            _searchField = new TextField
            {
                value = _searchText,
                style = { flexGrow = 1, height = 48, fontSize = 22 },
            };
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchText = evt.newValue ?? "";
                RebuildBrowseList();
            });
            StyleTextInput(_searchField);
            searchRow.Add(_searchField);

            var refreshButton = MakeButton("⟳ Refresh", () => RefreshCloudTours().Forget(), ACCENT_COLOR);
            refreshButton.style.marginLeft = 10;
            searchRow.Add(refreshButton);

            // Sortable header row
            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    borderBottomWidth = 2,
                    borderBottomColor = new Color(1f, 1f, 1f, 0.25f),
                    paddingBottom = 4, marginBottom = 4,
                },
            };
            content.Add(header);

            header.Add(MakeHeaderButton("Tour Name", SortColumn.Name, 3f));
            header.Add(MakeHeaderButton("Author", SortColumn.Author, 2f));
            header.Add(MakeHeaderButton("Rating", SortColumn.Rating, 2f));
            header.Add(MakeHeaderButton("Downloads", SortColumn.Downloads, 1.2f));
            header.Add(MakeHeaderButton("Downloaded", SortColumn.Downloaded, 1.4f));

            _browseList = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            content.Add(_browseList);

            return content;
        }

        private static VisualElement BuildMyToursTab()
        {
            var content = new VisualElement { style = { flexGrow = 1 } };

            content.Add(new Label(
                "Upload your local tours to the cloud, and rate tours you have played. " +
                "Uploading a tour you already uploaded updates it in place.")
            {
                style = { color = MUTED_TEXT_COLOR, marginBottom = 8, whiteSpace = WhiteSpace.Normal },
            });

            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    borderBottomWidth = 2,
                    borderBottomColor = new Color(1f, 1f, 1f, 0.25f),
                    paddingBottom = 4, marginBottom = 4,
                },
            };
            content.Add(header);

            header.Add(MakeHeaderLabel("Tour Name", 3f));
            header.Add(MakeHeaderLabel("Author", 2f));
            header.Add(MakeHeaderLabel("Your Rating", 2f));
            // Space matching the upload button column width.
            header.Add(new VisualElement { style = { width = 130 } });

            _myToursList = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            content.Add(_myToursList);

            return content;
        }

        // ─── Tab / data management ───────────────────────────────────────────

        private static void SwitchTab(Tab tab)
        {
            _activeTab = tab;
            _browseContent.style.display = tab == Tab.Browse ? DisplayStyle.Flex : DisplayStyle.None;
            _myToursContent.style.display = tab == Tab.MyTours ? DisplayStyle.Flex : DisplayStyle.None;

            _browseTabButton.style.backgroundColor = tab == Tab.Browse ? ACCENT_COLOR : new Color(0.2f, 0.2f, 0.25f);
            _myToursTabButton.style.backgroundColor = tab == Tab.MyTours ? ACCENT_COLOR : new Color(0.2f, 0.2f, 0.25f);

            if (tab == Tab.MyTours)
            {
                RefreshLocalTours();
                RebuildMyToursList();
            }
        }

        private static void RefreshLocalTours()
        {
            // A single corrupt .tour file must not take down the whole menu —
            // GetAllTours() deserializes every file and throws on bad JSON.
            // This runs inside Open(), so an uncaught throw here would leave
            // the EventSystem disabled with no visible UI.
            try
            {
                _localTours = TourManager.GetAllTours()
                    .Where(t => t != null)
                    .OrderBy(t => t.TourName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load local tours: {ex}");
                SetError($"Failed to load local tours: {ex.Message}");
                _localTours = new List<TourData>();
            }

            _localTourIds.Clear();
            foreach (var tour in _localTours)
            {
                _localTourIds.Add(tour.TourId.ToString());
            }
        }

        private static bool IsDownloaded(CloudTourMetadata tour)
        {
            return tour.Id != null && _localTourIds.Contains(tour.Id);
        }

        private static async UniTask RefreshCloudTours()
        {
            int generation = _openGeneration;
            SetStatus("Loading cloud tours…");
            try
            {
                var tours = await TourCloudService.GetTourList();
                if (IsStale(generation))
                {
                    return;
                }

                _cloudTours = tours;
                SetStatus($"{_cloudTours.Count} tour(s) available.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load cloud tours: {ex}");
                if (IsStale(generation))
                {
                    return;
                }

                SetError($"Failed to load cloud tours: {ex.Message}");
                _cloudTours = new List<CloudTourMetadata>();
            }

            RebuildBrowseList();
            RebuildMyToursList();
        }

        private static bool IsStale(int generation)
        {
            return !_isOpen || generation != _openGeneration;
        }

        // ─── Browse list ─────────────────────────────────────────────────────

        private static void RebuildBrowseList()
        {
            if (_browseList == null)
            {
                return;
            }

            _browseList.Clear();

            IEnumerable<CloudTourMetadata> tours = _cloudTours;

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                string filter = _searchText.Trim();
                tours = tours.Where(t =>
                    (t.Name?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (t.Author?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
            }

            tours = _sortColumn switch
            {
                SortColumn.Name       => Order(tours, t => t.Name, StringComparer.OrdinalIgnoreCase),
                SortColumn.Author     => Order(tours, t => t.Author, StringComparer.OrdinalIgnoreCase),
                SortColumn.Rating     => Order(tours, t => t.AverageRating),
                SortColumn.Downloads  => Order(tours, t => t.Downloads),
                SortColumn.Downloaded => Order(tours, t => IsDownloaded(t)),
                _                     => tours,
            };

            int index = 0;
            foreach (var tour in tours)
            {
                _browseList.Add(MakeBrowseRow(tour, index++));
            }

            if (index == 0)
            {
                _browseList.Add(new Label(_cloudTours.Count == 0
                    ? "No tours on the cloud yet."
                    : "No tours match your search.")
                {
                    style = { color = MUTED_TEXT_COLOR, marginTop = 15, alignSelf = Align.Center },
                });
            }

            UpdateHeaderButtonLabels();
        }

        private static IEnumerable<CloudTourMetadata> Order<TKey>(
            IEnumerable<CloudTourMetadata> tours, Func<CloudTourMetadata, TKey> key, IComparer<TKey> comparer = null)
        {
            return _sortAscending
                ? tours.OrderBy(key, comparer ?? Comparer<TKey>.Default)
                : tours.OrderByDescending(key, comparer ?? Comparer<TKey>.Default);
        }

        private static VisualElement MakeBrowseRow(CloudTourMetadata tour, int index)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    backgroundColor = index % 2 == 0 ? ROW_EVEN_COLOR : ROW_ODD_COLOR,
                    paddingTop = 6, paddingBottom = 6, paddingLeft = 4, paddingRight = 4,
                },
            };

            bool downloaded = IsDownloaded(tour);

            row.Add(MakeCell(tour.Name, 3f));
            row.Add(MakeCell(tour.Author, 2f));
            row.Add(MakeRatingCell(tour, downloaded, 2f));
            row.Add(MakeCell(tour.Downloads.ToString(), 1.2f));

            // Downloaded column: if the tour is already on disk there is no
            // download button — just say so.
            var downloadedCell = new VisualElement
            {
                style = { flexGrow = 1.4f, flexBasis = 0, flexDirection = FlexDirection.Row, alignItems = Align.Center },
            };
            row.Add(downloadedCell);

            if (downloaded)
            {
                downloadedCell.Add(new Label("✓ Downloaded")
                {
                    style = { color = new Color(0.2f, 0.85f, 0.3f), fontSize = 18, paddingLeft = 4 },
                });
            }
            else
            {
                var downloadButton = MakeButton("Download", () => DownloadTour(tour).Forget(),
                    new Color(0.2f, 0.85f, 0.3f));
                downloadButton.style.width = 130;
                downloadedCell.Add(downloadButton);
            }

            return row;
        }

        // Average rating text always shows. If you have the tour on disk you
        // can also set/update your own rating right here with the stars.
        private static VisualElement MakeRatingCell(CloudTourMetadata tour, bool downloaded, float flex)
        {
            var cell = new VisualElement
            {
                style = { flexGrow = flex, flexBasis = 0, flexDirection = FlexDirection.Row, alignItems = Align.Center },
            };

            string average = tour.RatingCount > 0
                ? $"{tour.AverageRating:0.0} ★ ({tour.RatingCount})"
                : "Unrated";

            var averageLabel = new Label(average)
            {
                style =
                {
                    fontSize = 18,
                    paddingLeft = 4,
                    color = tour.RatingCount > 0 ? STAR_COLOR : MUTED_TEXT_COLOR,
                    minWidth = 110,
                },
            };
            cell.Add(averageLabel);

            if (downloaded)
            {
                int userRating = TourCloudService.GetLocalRating(tour.Id);
                for (int star = 1; star <= 5; star++)
                {
                    cell.Add(MakeStarButton(tour, star, userRating));
                }
            }

            return cell;
        }

        private static Button MakeStarButton(CloudTourMetadata tour, int stars, int userRating)
        {
            return new Button(() => RateTour(tour, stars).Forget())
            {
                text = stars <= userRating ? "★" : "☆",
                style =
                {
                    fontSize = 24,
                    color = STAR_COLOR,
                    backgroundColor = Color.clear,
                    borderTopWidth = 0, borderBottomWidth = 0,
                    borderLeftWidth = 0, borderRightWidth = 0,
                    paddingLeft = 2, paddingRight = 2, paddingTop = 0, paddingBottom = 0,
                    marginLeft = 0, marginRight = 0,
                },
            };
        }

        // ─── My Tours list ───────────────────────────────────────────────────

        private static void RebuildMyToursList()
        {
            if (_myToursList == null)
            {
                return;
            }

            _myToursList.Clear();

            var cloudById = _cloudTours
                .Where(t => t.Id != null)
                .ToDictionary(t => t.Id, t => t, StringComparer.OrdinalIgnoreCase);

            int index = 0;
            foreach (var tour in _localTours)
            {
                cloudById.TryGetValue(tour.TourId.ToString(), out var cloudMeta);
                _myToursList.Add(MakeMyTourRow(tour, cloudMeta, index++));
            }

            if (index == 0)
            {
                _myToursList.Add(new Label("You have no local tours. Create one from the tour list first.")
                {
                    style = { color = MUTED_TEXT_COLOR, marginTop = 15, alignSelf = Align.Center },
                });
            }
        }

        private static VisualElement MakeMyTourRow(TourData tour, CloudTourMetadata cloudMeta, int index)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    backgroundColor = index % 2 == 0 ? ROW_EVEN_COLOR : ROW_ODD_COLOR,
                    paddingTop = 6, paddingBottom = 6, paddingLeft = 4, paddingRight = 4,
                },
            };

            row.Add(MakeCell(tour.TourName, 3f));
            row.Add(MakeCell(tour.Author, 2f));

            // Star rating (only meaningful once the tour exists on the cloud).
            var starsCell = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexGrow = 2f, flexBasis = 0, alignItems = Align.Center },
            };
            row.Add(starsCell);

            if (cloudMeta != null)
            {
                int userRating = TourCloudService.GetLocalRating(cloudMeta.Id);
                for (int star = 1; star <= 5; star++)
                {
                    starsCell.Add(MakeStarButton(cloudMeta, star, userRating));
                }
            }
            else
            {
                starsCell.Add(new Label("Not on cloud")
                {
                    style = { color = MUTED_TEXT_COLOR },
                });
            }

            string uploadLabel = cloudMeta != null ? "Update" : "Upload";
            var uploadButton = MakeButton(uploadLabel, () => UploadTour(tour).Forget(), ACCENT_COLOR);
            uploadButton.style.width = 130;
            row.Add(uploadButton);

            return row;
        }

        // ─── Actions ─────────────────────────────────────────────────────────

        private static async UniTaskVoid DownloadTour(CloudTourMetadata meta)
        {
            if (_busy)
            {
                return;
            }
            _busy = true;
            int generation = _openGeneration;

            try
            {
                SetStatus($"Downloading '{meta.Name}'…");

                string json = await TourCloudService.DownloadTourJson(meta.Id);
                var tour = JsonConvert.DeserializeObject<TourData>(json);
                if (tour == null || tour.TourId == Guid.Empty)
                {
                    throw new InvalidOperationException("Downloaded tour JSON was malformed.");
                }

                if (IsStale(generation))
                {
                    return;
                }

                // Warn about any songs the local library is missing BEFORE
                // saving anything to disk.
                var missing = FindMissingSongs(tour);
                if (missing.Count > 0)
                {
                    bool proceed = await ShowConfirmModal(
                        "MISSING SONGS",
                        $"Your library is missing {missing.Count} of the songs in '{meta.Name}':\n\n" +
                        string.Join("\n", missing.Take(15)) +
                        (missing.Count > 15 ? $"\n…and {missing.Count - 15} more" : "") +
                        "\n\nDownload the tour anyway?",
                        "Download Anyway");

                    if (!proceed || IsStale(generation))
                    {
                        SetStatus("Download cancelled.");
                        return;
                    }
                }

                string path = TourEditorMenu.SaveTourToFile(tour);
                Debug.Log($"Saved cloud tour '{tour.TourName}' to {path}");

                // The tour is on disk from this point on, so tell the tour
                // list to refresh no matter what happens to the count request
                // below.
                RequestViewListUpdate?.Invoke();

                // Only count the download once the tour is actually on disk.
                int downloads = await TourCloudService.RecordDownload(meta.Id);

                if (IsStale(generation))
                {
                    return;
                }

                meta.Downloads = downloads;
                SetStatus($"Downloaded '{tour.TourName}'.");
                // Refresh the local id set first so the browse row flips to
                // "✓ Downloaded" immediately.
                RefreshLocalTours();
                RebuildBrowseList();
                RebuildMyToursList();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to download tour '{meta.Name}': {ex}");
                if (!IsStale(generation))
                {
                    SetError($"Failed to download '{meta.Name}': {ex.Message}");
                }
            }
            finally
            {
                // Only release the busy lock if this action still belongs to
                // the current session — otherwise a request that finished after
                // a close/reopen would unlock someone else's in-flight action.
                if (generation == _openGeneration)
                {
                    _busy = false;
                }
            }
        }

        private static async UniTaskVoid UploadTour(TourData tour)
        {
            if (_busy)
            {
                return;
            }
            _busy = true;
            int generation = _openGeneration;

            try
            {
                SetStatus($"Uploading '{tour.TourName}'…");
                await TourCloudService.UploadTour(tour);
                if (IsStale(generation))
                {
                    return;
                }

                SetStatus($"Uploaded '{tour.TourName}'.");

                // Refresh so the new/updated tour shows up in the browse tab
                // and gets rating stars in the My Tours tab.
                await RefreshCloudTours();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to upload tour '{tour.TourName}': {ex}");
                if (!IsStale(generation))
                {
                    SetError($"Failed to upload '{tour.TourName}': {ex.Message}");
                }
            }
            finally
            {
                if (generation == _openGeneration)
                {
                    _busy = false;
                }
            }
        }

        private static async UniTaskVoid RateTour(CloudTourMetadata meta, int stars)
        {
            if (_busy)
            {
                return;
            }
            _busy = true;
            int generation = _openGeneration;

            try
            {
                SetStatus($"Rating '{meta.Name}' {stars} star(s)…");
                (meta.RatingSum, meta.RatingCount) = await TourCloudService.RateTour(meta.Id, stars);
                if (IsStale(generation))
                {
                    return;
                }

                SetStatus($"Rated '{meta.Name}' {stars} star(s).");

                RebuildMyToursList();
                RebuildBrowseList();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to rate tour '{meta.Name}': {ex}");
                if (!IsStale(generation))
                {
                    SetError($"Failed to rate '{meta.Name}': {ex.Message}");
                }
            }
            finally
            {
                if (generation == _openGeneration)
                {
                    _busy = false;
                }
            }
        }

        private static List<string> FindMissingSongs(TourData tour)
        {
            var missing = new List<string>();
            if (tour.Shows == null)
            {
                return missing;
            }

            foreach (var show in tour.Shows)
            {
                if (show?.Songs == null)
                {
                    continue;
                }

                foreach (var song in show.Songs)
                {
                    if (SongContainer.GetFuzzySongEntry(song.Artist, song.SongName) == null)
                    {
                        string label = $"{song.Artist} - {song.SongName}";
                        if (!missing.Contains(label))
                        {
                            missing.Add(label);
                        }
                    }
                }
            }

            return missing;
        }

        // ─── Input shield ────────────────────────────────────────────────────

        private static void RaiseInputShield()
        {
            if (_inputShield != null || _root == null)
            {
                return;
            }

            _inputShield = new VisualElement
            {
                // Fully transparent, but picks (and therefore swallows) every
                // pointer event before it can reach anything underneath.
                pickingMode = PickingMode.Position,
                style =
                {
                    position = Position.Absolute,
                    left = 0, right = 0, top = 0, bottom = 0,
                },
            };
            _root.Add(_inputShield);
        }

        private static async UniTaskVoid LowerInputShieldWhenPointerReleased()
        {
            int generation = _openGeneration;

            // Always skip at least one frame so the frame that opened the menu
            // can never interact with it, then wait for every pressed pointer
            // button to be released.
            do
            {
                await UniTask.Yield();

                if (IsStale(generation))
                {
                    return;
                }
            } while (IsAnyPointerButtonPressed());

            _inputShield?.RemoveFromHierarchy();
            _inputShield = null;
        }

        private static bool IsAnyPointerButtonPressed()
        {
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.isPressed
                || mouse.rightButton.isPressed
                || mouse.middleButton.isPressed))
            {
                return true;
            }

            var touch = Touchscreen.current;
            return touch != null && touch.primaryTouch.press.isPressed;
        }

        // ─── Modal confirm ───────────────────────────────────────────────────

        private static UniTask<bool> ShowConfirmModal(string title, string message, string confirmText)
        {
            // Only one modal can exist at a time.
            if (_modalOverlay != null)
            {
                ResolveModal(false);
            }

            _modalCompletion = new UniTaskCompletionSource<bool>();

            _modalOverlay = new VisualElement
            {
                pickingMode = PickingMode.Position,
                style =
                {
                    position = Position.Absolute,
                    left = 0, right = 0, top = 0, bottom = 0,
                    backgroundColor = new Color(0f, 0f, 0f, 0.7f),
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                },
            };

            var box = new VisualElement
            {
                style =
                {
                    width = 700,
                    maxHeight = Length.Percent(80),
                    backgroundColor = PANEL_COLOR,
                    borderTopLeftRadius = 10, borderTopRightRadius = 10,
                    borderBottomLeftRadius = 10, borderBottomRightRadius = 10,
                    paddingLeft = 25, paddingRight = 25, paddingTop = 20, paddingBottom = 20,
                },
            };
            _modalOverlay.Add(box);

            box.Add(new Label(title)
            {
                style =
                {
                    fontSize = 26, unityFontStyleAndWeight = FontStyle.Bold,
                    color = new Color(1f, 0.75f, 0.25f), marginBottom = 12,
                },
            });

            var messageScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                style = { maxHeight = 400, marginBottom = 15 },
            };
            messageScroll.Add(new Label(message)
            {
                style = { whiteSpace = WhiteSpace.Normal, fontSize = 18 },
            });
            box.Add(messageScroll);

            var buttonRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, justifyContent = Justify.Center },
            };
            box.Add(buttonRow);

            var confirmButton = MakeButton(confirmText, () => ResolveModal(true), new Color(0.2f, 0.85f, 0.3f));
            confirmButton.style.marginRight = 20;
            buttonRow.Add(confirmButton);
            buttonRow.Add(MakeButton("Cancel", () => ResolveModal(false), new Color(0.9f, 0.25f, 0.25f)));

            _root.Add(_modalOverlay);

            if (_backBarButton != null)
            {
                _backBarButton.text = "● Back — Cancel";
            }

            return _modalCompletion.Task;
        }

        private static void ResolveModal(bool result)
        {
            _modalOverlay?.RemoveFromHierarchy();
            _modalOverlay = null;

            if (_backBarButton != null)
            {
                _backBarButton.text = "● Back — Close Cloud Tours";
            }

            var completion = _modalCompletion;
            _modalCompletion = null;
            completion?.TrySetResult(result);
        }

        // ─── Sorting header ──────────────────────────────────────────────────

        private static Button MakeHeaderButton(string text, SortColumn column, float flex)
        {
            var button = new Button(() => CycleSort(column))
            {
                text = text,
                style =
                {
                    flexGrow = flex, flexBasis = 0,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 18,
                    color = TEXT_COLOR,
                    backgroundColor = Color.clear,
                    borderTopWidth = 0, borderBottomWidth = 0,
                    borderLeftWidth = 0, borderRightWidth = 0,
                    marginLeft = 0, marginRight = 0,
                    paddingLeft = 4,
                },
            };

            _headerButtons[column] = button;
            return button;
        }

        private static Label MakeHeaderLabel(string text, float flex)
        {
            return new Label(text)
            {
                style =
                {
                    flexGrow = flex, flexBasis = 0,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 18,
                    paddingLeft = 4,
                },
            };
        }

        private static void CycleSort(SortColumn column)
        {
            // Cycle: none → ascending → descending → none.
            if (_sortColumn != column)
            {
                _sortColumn = column;
                _sortAscending = true;
            }
            else if (_sortAscending)
            {
                _sortAscending = false;
            }
            else
            {
                _sortColumn = SortColumn.None;
                _sortAscending = true;
            }

            RebuildBrowseList();
        }

        private static void UpdateHeaderButtonLabels()
        {
            foreach (var (column, button) in _headerButtons)
            {
                string baseText = column switch
                {
                    SortColumn.Name       => "Tour Name",
                    SortColumn.Author     => "Author",
                    SortColumn.Rating     => "Rating",
                    SortColumn.Downloads  => "Downloads",
                    SortColumn.Downloaded => "Downloaded",
                    _                     => "",
                };

                if (_sortColumn == column)
                {
                    button.text = baseText + (_sortAscending ? " ▲" : " ▼");
                    button.style.color = ACCENT_COLOR;
                }
                else
                {
                    button.text = baseText;
                    button.style.color = TEXT_COLOR;
                }
            }
        }

        // ─── Small element factories ─────────────────────────────────────────

        private static Label MakeCell(string text, float flex)
        {
            return new Label(text ?? "")
            {
                style =
                {
                    flexGrow = flex, flexBasis = 0,
                    fontSize = 18,
                    paddingLeft = 4,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    whiteSpace = WhiteSpace.NoWrap,
                },
            };
        }

        private static Button MakeButton(string text, Action onClick, Color background)
        {
            return new Button(onClick)
            {
                text = text,
                style =
                {
                    fontSize = 17,
                    color = Color.white,
                    backgroundColor = background,
                    paddingLeft = 14, paddingRight = 14, paddingTop = 6, paddingBottom = 6,
                    borderTopLeftRadius = 5, borderTopRightRadius = 5,
                    borderBottomLeftRadius = 5, borderBottomRightRadius = 5,
                    borderTopWidth = 0, borderBottomWidth = 0,
                    borderLeftWidth = 0, borderRightWidth = 0,
                },
            };
        }

        private static void StyleTextInput(TextField field)
        {
            field.style.color = TEXT_COLOR;
            var input = field.Q("unity-text-input");
            if (input != null)
            {
                input.style.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
                input.style.color = TEXT_COLOR;
                input.style.paddingLeft = 8;
                input.style.borderTopLeftRadius = 5;
                input.style.borderTopRightRadius = 5;
                input.style.borderBottomLeftRadius = 5;
                input.style.borderBottomRightRadius = 5;
            }
        }

        private static void SetStatus(string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = message;
                _statusLabel.style.color = MUTED_TEXT_COLOR;
            }
        }

        private static void SetError(string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = message;
                _statusLabel.style.color = ERROR_COLOR;
            }
        }
    }
}

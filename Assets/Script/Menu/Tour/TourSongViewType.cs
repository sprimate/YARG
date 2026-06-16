using TMPro;
using UnityEngine.UI;
using YARG.Core.Song;
using YARG.Menu.MusicLibrary;

public class TourSongViewType : SongViewType
{
    protected override bool ShowAllInstrumentHighScores => true;

    public bool isLocked;
    public TourSongViewType(MusicLibraryMenu musicLibrary, SongEntry songEntry, bool _isLocked) : base(musicLibrary, songEntry)
    {
        isLocked = _isLocked;
        FetchHighScores();
    }

    public override void PrimaryButtonClick()
    {
        if (isLocked)
        {
            return;
        }

        base.PrimaryButtonClick();
    }
}
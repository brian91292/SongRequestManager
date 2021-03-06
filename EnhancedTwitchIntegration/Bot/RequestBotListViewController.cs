﻿using CustomUI.BeatSaber;
using StreamCore.Config;
using StreamCore.Utils;
using HMUI;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUI;
using Image = UnityEngine.UI.Image;
using System.IO;
using StreamCore.Chat;
using SongRequestManager;
using SongCore;
using BeatSaverDownloader;
using SongRequestManager.UI;

namespace SongRequestManager
{
 
    public class RequestBotListViewController : CustomListViewController
    {
        public static RequestBotListViewController Instance;

        private CustomMenu _confirmationDialog;
        private CustomViewController _confirmationViewController;

        public CustomMenu _KeyboardDialog;

        private LevelListTableCell _songListTableCellInstance;
        private SongPreviewPlayer _songPreviewPlayer;
        private Button _playButton, _skipButton, _blacklistButton, _historyButton, _okButton, _cancelButton, _queueButton;

        private TextMeshProUGUI _warningTitle, _warningMessage,_CurrentSongName,_CurrentSongName2;
        private LevelListTableCell _requestListTableCellInstance;
        private HoverHint _historyHintText;
        private int _requestRow = 0;
        private int _historyRow = 0;
        private int _lastSelection = -1;
        private int _selectedRow
        {
            get { return isShowingHistory ? _historyRow : _requestRow; }
            set
            {
                if (isShowingHistory)
                    _historyRow = value;
                else
                    _requestRow = value;
            }
        }
        private Action _onConfirm;
        private bool isShowingHistory = false;
        private bool confirmDialogActive = false;

        private KEYBOARD CenterKeys;

        string SONGLISTKEY = @"
[blacklist last]/0'!block/current%CR%'

[fun +]/25'!fun/current/toggle%CR%' [hard +]/25'!hard/current/toggle%CR%'
[dance +]/25'!dance/current/toggle%CR%' [chill +]/25'!chill/current/toggle%CR%'
[brutal +]/25'!brutal/current/toggle%CR%' [sehria +]/25'!sehria/current/toggle%CR%'

[rock +]/25'!rock/current/toggle%CR%' [metal +]/25'!metal/current/toggle%CR%'  
[anime +]/25'!anime/current/toggle%CR%' [pop +]/25'!pop/current/toggle%CR%' 

[Random song!]/0'!decklist draw%CR%'";

        public static void InvokeBeatSaberButton(String buttonName)
        {
            Button buttonInstance = Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == buttonName));
            buttonInstance.onClick.Invoke();
        }

        public void Awake()
        {
            Instance = this;
        }

        public void ColorDeckButtons(KEYBOARD kb,Color basecolor,Color Present)
            {
            if (RequestHistory.Songs.Count == 0) return;
            foreach (KEYBOARD.KEY key in kb.keys)
                foreach (var item in RequestBot.deck)
                {
                    string search = $"!{item.Key}/selected/toggle";
                    if (key.value.StartsWith(search))
                        {
                        string deckname = item.Key.ToLower() + ".deck";
                        Color color= (RequestBot.listcollection.contains(ref deckname, CurrentlySelectedSong().song["id"].Value)) ? Present : basecolor;
                        key.mybutton.GetComponentInChildren<Image>().color =color;
                        }
                }   
            }

        static public SongRequest currentsong = null;

        //static bool test(string x)
        //{
        //    File.AppendAllText("c:\\sehria\\objects.txt", x + "\r\n");
        //    return false;
        //}
        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            if (firstActivation)
            {
                if (!SongCore.Loader.AreSongsLoaded)
                    SongCore.Loader.SongsLoadedEvent += SongLoader_SongsLoadedEvent;

                // get table cell instance
                _requestListTableCellInstance = Resources.FindObjectsOfTypeAll<LevelListTableCell>().First((LevelListTableCell x) => x.name == "LevelListTableCell");

                InitConfirmationDialog();

                //Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Any(x => (test(x.name))); ;

                _songListTableCellInstance = Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(o => (o.name == "LevelListTableCell"));
                _songPreviewPlayer = Resources.FindObjectsOfTypeAll<SongPreviewPlayer>().FirstOrDefault();
                DidSelectRowEvent += DidSelectRow;

                RectTransform container = new GameObject("RequestBotContainer", typeof(RectTransform)).transform as RectTransform;
                container.SetParent(rectTransform, false);
                container.sizeDelta = new Vector2(60f, 0f);

                try
                {
                    InitKeyboardDialog();
                }
                catch (Exception ex)
                {
                    Plugin.Log(ex.ToString());
                }

                CenterKeys = new KEYBOARD(container, "", false, -15, 15);

#if UNRELEASED
                // BUG: Need additional modes disabling one shot buttons
                // BUG: Need to make sure the buttons are usable on older headsets

                _CurrentSongName = BeatSaberUI.CreateText(container, "", new Vector2(-35, 37f));
                _CurrentSongName.fontSize = 3f;
                _CurrentSongName.color = Color.cyan;
                _CurrentSongName.alignment = TextAlignmentOptions.Left;
                _CurrentSongName.enableWordWrapping = false;
                _CurrentSongName.text = "";

                _CurrentSongName2 = BeatSaberUI.CreateText(container, "", new Vector2(-35, 34f));
                _CurrentSongName2.fontSize = 3f;
                _CurrentSongName2.color = Color.cyan;
                _CurrentSongName2.alignment = TextAlignmentOptions.Left;
                _CurrentSongName2.enableWordWrapping = false;
                _CurrentSongName2.text = "";
                
                //CenterKeys.AddKeys(SONGLISTKEY);
                RequestBot.AddKeyboard(CenterKeys, "mainpanel.kbd");
                ColorDeckButtons(CenterKeys, Color.white, Color.magenta);
#endif

                RequestBot.AddKeyboard(CenterKeys, "CenterPanel.kbd");

                CenterKeys.DefaultActions();

                // History button
                _historyButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(o => (o.name == "OkButton")), container, false);
                _historyButton.ToggleWordWrapping(false);
                (_historyButton.transform as RectTransform).anchoredPosition = new Vector2(90f, 30f);
                _historyButton.SetButtonText("History");
                _historyButton.onClick.RemoveAllListeners();
                _historyButton.onClick.AddListener(delegate ()
                {
                    isShowingHistory = !isShowingHistory;
                    Resources.FindObjectsOfTypeAll<VRUIScreenSystem>().First().title = isShowingHistory ? "Song Request History" : "Song Request Queue";
                    UpdateRequestUI(true);
                    SetUIInteractivity();
                    _lastSelection = -1;
                });
                _historyHintText = BeatSaberUI.AddHintText(_historyButton.transform as RectTransform, "");
                
                // Blacklist button
                _blacklistButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(o => (o.name == "OkButton")), container, false);
                _blacklistButton.ToggleWordWrapping(false);
                (_blacklistButton.transform as RectTransform).anchoredPosition = new Vector2(90f, 10f);
                _blacklistButton.SetButtonText("Blacklist");
                //_blacklistButton.GetComponentInChildren<Image>().color = Color.red;
                _blacklistButton.onClick.RemoveAllListeners();
                _blacklistButton.onClick.AddListener(delegate ()
                {
                    if (NumberOfCells() > 0)
                    {
                        _onConfirm = () =>
                        {
                            RequestBot.Blacklist(_selectedRow, isShowingHistory, true);
                            if (_selectedRow > 0)
                                _selectedRow--;
                        };
                        var song = SongInfoForRow(_selectedRow).song;
                        _warningTitle.text = "Blacklist Song Warning";
                        _warningMessage.text = $"Blacklisting {song["songName"].Value} by {song["authorName"].Value}\r\nDo you want to continue?";
                        confirmDialogActive = true;
                        _confirmationDialog.Present();
                    }
                });
                BeatSaberUI.AddHintText(_blacklistButton.transform as RectTransform, "Block the selected request from being queued in the future.");

                // Skip button
                _skipButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(o => (o.name == "OkButton")), container, false);
                _skipButton.ToggleWordWrapping(false);
                (_skipButton.transform as RectTransform).anchoredPosition = new Vector2(90f, 0f);
                _skipButton.SetButtonText("Skip");
                //_skipButton.GetComponentInChildren<Image>().color = Color.yellow;
                _skipButton.onClick.RemoveAllListeners();
                _skipButton.onClick.AddListener(delegate ()
                {
                    if (NumberOfCells() > 0)
                    {
                        _onConfirm = () =>
                        {
                            currentsong = SongInfoForRow(_selectedRow);
                            RequestBot.Skip(_selectedRow);
                            if (_selectedRow > 0)
                                _selectedRow--;
                        };
                        var song = SongInfoForRow(_selectedRow).song;
                        _warningTitle.text = "Skip Song Warning";
                        _warningMessage.text = $"Skipping {song["songName"].Value} by {song["authorName"].Value}\r\nDo you want to continue?";
                        confirmDialogActive = true;
                        _confirmationDialog.Present();
                    }
                });
                BeatSaberUI.AddHintText(_skipButton.transform as RectTransform, "Remove the selected request from the queue.");

                // Play button
                _playButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(o => (o.name == "OkButton")), container, false);
                _playButton.ToggleWordWrapping(false);
                (_playButton.transform as RectTransform).anchoredPosition = new Vector2(90f, -10f);
                _playButton.SetButtonText("Play");
                _playButton.GetComponentInChildren<Image>().color = Color.green;
                _playButton.onClick.RemoveAllListeners();
                _playButton.onClick.AddListener(delegate ()
                {
                    if (NumberOfCells() > 0)
                    {
                        currentsong = SongInfoForRow(_selectedRow);
                        RequestBot.played.Add(currentsong.song);
                        RequestBot.WriteJSON(RequestBot.playedfilename, ref RequestBot.played);
                        
                        SetUIInteractivity(false);
                        RequestBot.Process(_selectedRow, isShowingHistory);
                        _selectedRow = -1;
                    }
                });
                BeatSaberUI.AddHintText(_playButton.transform as RectTransform, "Download and scroll to the currently selected request.");

                // Queue button
                _queueButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(o => (o.name == "OkButton")), container, false);
                _queueButton.ToggleWordWrapping(false);
                _queueButton.SetButtonTextSize(3.5f);
                (_queueButton.transform as RectTransform).anchoredPosition = new Vector2(90f, -30f);
                _queueButton.SetButtonText(RequestBotConfig.Instance.RequestQueueOpen ? "Queue Open" : "Queue Closed");
                _queueButton.GetComponentInChildren<Image>().color = RequestBotConfig.Instance.RequestQueueOpen ? Color.green : Color.red; ;
                _queueButton.interactable = true;
                _queueButton.onClick.RemoveAllListeners();
                _queueButton.onClick.AddListener(delegate ()
                {
                    RequestBotConfig.Instance.RequestQueueOpen = !RequestBotConfig.Instance.RequestQueueOpen;
                    RequestBotConfig.Instance.Save();
                    RequestBot.WriteQueueStatusToFile(RequestBotConfig.Instance.RequestQueueOpen ? "Queue is open." : "Queue is closed.");
                    RequestBot.Instance.QueueChatMessage(RequestBotConfig.Instance.RequestQueueOpen ? "Queue is open." : "Queue is closed.");
                    UpdateRequestUI();
                });
                BeatSaberUI.AddHintText(_queueButton.transform as RectTransform, "Open/Close the queue.");
            }
            base.DidActivate(firstActivation, type);
            UpdateRequestUI();
            SetUIInteractivity(true);
        }

        protected override void DidDeactivate(DeactivationType type)
        {
            base.DidDeactivate(type);
            if (!confirmDialogActive)
                isShowingHistory = false;
        }

        public SongRequest CurrentlySelectedSong()
        {
            var currentsong = RequestHistory.Songs[0];

            if (_selectedRow != -1 && NumberOfCells() > _selectedRow)
            {
                currentsong = SongInfoForRow(_selectedRow);
            }
            return currentsong;
        }


        public void UpdateSelectSongInfo()
            {
#if UNRELEASED
            if (RequestHistory.Songs.Count > 0)
            {

                var currentsong = CurrentlySelectedSong();

                _CurrentSongName.text = currentsong.song["songName"].Value;
                _CurrentSongName2.text = $"{currentsong.song["authorName"].Value} ({currentsong.song["version"].Value})";

                ColorDeckButtons(CenterKeys, Color.white, Color.magenta);
            }
#endif

        }

        public void UpdateRequestUI(bool selectRowCallback = false)
        {
            _playButton.GetComponentInChildren<Image>().color = ((isShowingHistory && RequestHistory.Songs.Count > 0) || (!isShowingHistory && RequestQueue.Songs.Count > 0)) ? Color.green : Color.red;
            _queueButton.SetButtonText(RequestBotConfig.Instance.RequestQueueOpen ? "Queue Open" : "Queue Closed");
            _queueButton.GetComponentInChildren<Image>().color = RequestBotConfig.Instance.RequestQueueOpen ? Color.green : Color.red; ;
            _historyHintText.text = isShowingHistory ? "Go back to your current song request queue." : "View the history of song requests from the current session.";
            _historyButton.SetButtonText(isShowingHistory ? "Requests" : "History");
            _playButton.SetButtonText(isShowingHistory ? "Replay" : "Play");

            UpdateSelectSongInfo();


            _customListTableView.ReloadData();

            if (_selectedRow == -1) return;


            if (NumberOfCells() > _selectedRow)
            {
                _customListTableView.SelectCellWithIdx(_selectedRow, selectRowCallback);
                _customListTableView.ScrollToCellWithIdx(_selectedRow, TableViewScroller.ScrollPositionType.Beginning, true);
            }

            
        }

        private void InitKeyboardDialog()
        {         
        }

        private void InitConfirmationDialog()
        {
            _confirmationDialog = BeatSaberUI.CreateCustomMenu<CustomMenu>("Are you sure?");
            _confirmationViewController = BeatSaberUI.CreateViewController<CustomViewController>();

            RectTransform confirmContainer = new GameObject("CustomListContainer", typeof(RectTransform)).transform as RectTransform;
            confirmContainer.SetParent(_confirmationViewController.rectTransform, false);
            confirmContainer.sizeDelta = new Vector2(60f, 0f);

            // Title text
            _warningTitle = BeatSaberUI.CreateText(confirmContainer, "", new Vector2(0, 30f));
            _warningTitle.fontSize = 9f;
            _warningTitle.color = Color.red;
            _warningTitle.alignment = TextAlignmentOptions.Center;
            _warningTitle.enableWordWrapping = false;

            // Warning text
            _warningMessage = BeatSaberUI.CreateText(confirmContainer, "", new Vector2(0, 0));
            _warningMessage.rectTransform.sizeDelta = new Vector2(120, 1);
            _warningMessage.fontSize = 5f;
            _warningMessage.color = Color.white;
            _warningMessage.alignment = TextAlignmentOptions.Center;
            _warningMessage.enableWordWrapping = true;

            // Yes button
            _okButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "OkButton")), confirmContainer, false);
            _okButton.ToggleWordWrapping(false);
            (_okButton.transform as RectTransform).anchoredPosition = new Vector2(43f, -30f);
            _okButton.SetButtonText("Yes");
            _okButton.onClick.RemoveAllListeners();
            _okButton.onClick.AddListener(delegate ()
            {
                _onConfirm?.Invoke();
                _confirmationDialog.Dismiss();
                confirmDialogActive = false;
            });

            // No button
            _cancelButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "OkButton")), confirmContainer, false);
            _cancelButton.ToggleWordWrapping(false);
            (_cancelButton.transform as RectTransform).anchoredPosition = new Vector2(18f, -30f);
            _cancelButton.SetButtonText("No");
            _cancelButton.onClick.RemoveAllListeners();
            _cancelButton.onClick.AddListener(delegate ()
            {
                _confirmationDialog.Dismiss();
                confirmDialogActive = false;
            });
            _confirmationDialog.SetMainViewController(_confirmationViewController, false);
        }

        private void DidSelectRow(TableView table, int row)
        {
            _selectedRow = row;
            if (row != _lastSelection)
            {
                _lastSelection = row;
            }

      
            // if not in history, disable play button if request is a challenge
            if (!isShowingHistory)
            {
                var request = SongInfoForRow(row);
                var isChallenge = request.requestInfo.IndexOf("!challenge", StringComparison.OrdinalIgnoreCase) >= 0;
                _playButton.interactable = !isChallenge;
            }

            UpdateSelectSongInfo();

            SetUIInteractivity();
        }

        private void SongLoader_SongsLoadedEvent(SongCore.Loader arg1, Dictionary <string,CustomPreviewBeatmapLevel> arg2)
        {
            _customListTableView?.ReloadData();
        }

 
        /// <summary>
        /// Alter the state of the buttons based on selection
        /// </summary>
        /// <param name="interactive">Set to false to force disable all buttons, true to auto enable buttons based on states</param>
        public void SetUIInteractivity(bool interactive = true)
        {
            var toggled = interactive;
                        if (NumberOfCells() == 0 || _selectedRow == -1)
                            {
                Plugin.Log("Nothing selected, or empty list, buttons should be off");
                toggled = false;
                            }
            
            var playButtonEnabled = toggled;
                        if (toggled && !isShowingHistory)
                            {
                var request = SongInfoForRow(_selectedRow);
                var isChallenge = request.requestInfo.IndexOf("!challenge", StringComparison.OrdinalIgnoreCase) >= 0;
                playButtonEnabled = isChallenge ? false : toggled;
                            }
            
            _playButton.interactable = playButtonEnabled;
            
            var skipButtonEnabled = toggled;
                        if (toggled && isShowingHistory)
                            {
                skipButtonEnabled = false;
                            }
            _skipButton.interactable = skipButtonEnabled;
            
            _blacklistButton.interactable = toggled;

            // history button can be enabled even if others are disabled
            _historyButton.interactable = true;
            _historyButton.interactable = interactive;

            _playButton.interactable = interactive;
            _skipButton.interactable = interactive;
            _blacklistButton.interactable = interactive;
                        // history button can be enabled even if others are disabled
            _historyButton.interactable = true;
        }


        private CustomPreviewBeatmapLevel CustomLevelForRow(int row)
        {
            // get level id from hash
            var levelIds = SongCore.Collections.levelIDsForHash(SongInfoForRow(row).song["hash"]);
            if (levelIds.Count == 0) return null;
            if (levelIds.Count == 0) return null;
            
            // lookup song from level id
            return SongCore.Loader.CustomLevels.FirstOrDefault(s => string.Equals(s.Value.levelID, levelIds.First(), StringComparison.OrdinalIgnoreCase)).Value ?? null;
        }

        private SongRequest SongInfoForRow(int row)
        {
            return isShowingHistory ? RequestHistory.Songs.ElementAt(row) : RequestQueue.Songs.ElementAt(row);
        }

        private void PlayPreview(CustomPreviewBeatmapLevel level)
        {
            //_songPreviewPlayer.CrossfadeTo(level.previewAudioClip, level.previewStartTime, level.previewDuration);
        }

        private static Dictionary<string, Sprite> _cachedSprites = new Dictionary<string, Sprite>();
        public static Sprite GetSongCoverArt(string url, Action<Sprite> downloadCompleted)
        {
            if (!_cachedSprites.ContainsKey(url))
            {
                RequestBot.Instance.StartCoroutine(Utilities.DownloadSpriteAsync($"https://beatsaver.com{url}", downloadCompleted));
                _cachedSprites.Add(url, CustomUI.Utilities.UIUtilities.BlankSprite);
            }
            return _cachedSprites[url];
        }

        public override int NumberOfCells()
        {
            return isShowingHistory ? RequestHistory.Songs.Count() : RequestQueue.Songs.Count();
        }

        public override TableCell CellForIdx(TableView tableView, int row)
        {

            LevelListTableCell _tableCell = Instantiate(_requestListTableCellInstance);
            _tableCell.reuseIdentifier = "VersusPlaylistFriendCell";
            _tableCell.SetPrivateField("_bought", true);

            SongRequest request = SongInfoForRow(row);
            SetDataFromLevelAsync(request, _tableCell, row);

            return _tableCell;
        }

        private async void SetDataFromLevelAsync(SongRequest request, LevelListTableCell _tableCell, int row)
        {
            bool highlight = (request.requestInfo.Length > 0) && (request.requestInfo[0] == '!');

            string msg = highlight ? "MSG" : "";

            var hasMessage = (request.requestInfo.Length > 0) && (request.requestInfo[0] == '!');
            var isChallenge = request.requestInfo.IndexOf("!challenge", StringComparison.OrdinalIgnoreCase) >= 0;

            var beatmapCharacteristicImages = _tableCell.GetPrivateField<UnityEngine.UI.Image[]>("_beatmapCharacteristicImages"); // NEW VERSION
            foreach (var i in beatmapCharacteristicImages) i.enabled = false;
            _tableCell.SetPrivateField("_beatmapCharacteristicAlphas", new float[3] { 0f, 1f, 1f });
            // set message icon if request has a message // NEW VERSION
            if (hasMessage)
            {
                beatmapCharacteristicImages.Last().sprite = Base64Sprites.InfoIcon;
                beatmapCharacteristicImages.Last().enabled = true;
            }

            // set challenge icon if song is a challenge
            if (isChallenge)
            {
                var el = beatmapCharacteristicImages.ElementAt(1);

                el.sprite = Base64Sprites.VersusChallengeIcon;
                el.enabled = true;
            }

            string pp = "";
            int ppvalue = request.song["pp"].AsInt;
            if (ppvalue > 0) pp = $" {ppvalue} PP";

            var dt = new RequestBot.DynamicText().AddSong(request.song).AddUser(ref request.requestor); // Get basic fields
            dt.Add("Status", request.status.ToString());
            dt.Add("Info", (request.requestInfo != "") ? " / " + request.requestInfo : "");
            dt.Add("RequestTime", request.requestTime.ToLocalTime().ToString("hh:mm"));

            var songName = _tableCell.GetPrivateField<TextMeshProUGUI>("_songNameText");
            //songName.text = $"{request.song["songName"].Value} <size=50%>{RequestBot.GetRating(ref request.song)} <color=#3fff3f>{pp}</color></size> <color=#ff00ff>{msg}</color>";
            songName.text = $"{request.song["songName"].Value} <size=50%>{RequestBot.GetRating(ref request.song)} <color=#3fff3f>{pp}</color></size>"; // NEW VERSION

            var author = _tableCell.GetPrivateField<TextMeshProUGUI>("_authorText");

            author.text = dt.Parse(RequestBot.QueueListRow2);

            var image = _tableCell.GetPrivateField<RawImage>("_coverRawImage");
            var imageSet = false;

            if (SongCore.Loader.AreSongsLoaded)
            {
                CustomPreviewBeatmapLevel level = CustomLevelForRow(row);
                if (level != null)
                {
                    Plugin.Log("custom level found");
                                        // set image from song's cover image
                    var tex = await level.GetCoverImageTexture2DAsync(System.Threading.CancellationToken.None);
                    image.texture = tex;
                    imageSet = true;
                }
            }

            if (!imageSet)
            {
                string url = request.song["coverURL"].Value;
                var s = GetSongCoverArt(url, (sprite) => { _cachedSprites[url] = sprite; _customListTableView.ReloadData(); });
                image.texture = s.texture;
            }

            BeatSaberUI.AddHintText(_tableCell.transform as RectTransform, dt.Parse(RequestBot.SongHintText));
        }
    }
}
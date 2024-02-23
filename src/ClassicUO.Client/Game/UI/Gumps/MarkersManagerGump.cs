using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClassicUO.Resources;
using Microsoft.Xna.Framework.Graphics;
using static ClassicUO.Game.UI.Gumps.WorldMapGump;
using ClassicUO.Renderer;
using ClassicUO.Game.GameObjects;
using ClassicUO.Network;
using System.Text.RegularExpressions;
using ClassicUO.Game.Data;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class MarkersManagerGump : Gump
    {
        private enum ButtonsOption
        {
            SEARCH_BTN = 100,
            CLEAR_SEARCH_BTN,
            IMPORT_SOS,
            IMPORT_TMAP,
        }

        private const int WIDTH = 620;
        private const int HEIGHT = 500;

        private const ushort HUE_FONT = 0xFFFF;

        private static readonly List<WMapMarkerFile> _markerFiles = WorldMapGump.MarkerFiles;

        private bool _isMarkerListModified;

        private readonly ScrollArea _scrollArea;

        private readonly SearchTextBoxControl _searchTextBox;
        private readonly NiceButton _importSOSButton, _importTMapButton;

        private string _searchText = "";
        private int _categoryId = 0;

        private List<WMapMarker> _markers = [];

        private readonly string _userMarkersFilePath = Path.Combine(CUOEnviroment.ExecutablePath, "Data", "Client", $"{USER_MARKERS_FILE}.usr");

        private readonly int MARKERS_CATEGORY_GROUP_INDEX = 10;

        internal MarkersManagerGump() : base(0, 0)
        {
            X = 50;
            Y = 50;
            CanMove = true;
            AcceptMouseInput = true;

            var button_width = 50;

            if (_markerFiles.Count > 0)
            {
                _markers = _markerFiles[0].Markers;
                button_width = WIDTH / _markerFiles.Count;
            }

            Add(new AlphaBlendControl(0.95f)
            {
                X = 1,
                Y = 1,
                Width = WIDTH,
                Height = HEIGHT,
                Hue = 999,
                AcceptMouseInput = true,
                CanCloseWithRightClick = true,
                CanMove = true,
            });

            #region Border

            Add(new Line(0, 0, WIDTH, 1, Color.Gray.PackedValue));
            Add(new Line(0, 0, 1, HEIGHT, Color.Gray.PackedValue));
            Add(new Line(0, HEIGHT, WIDTH, 1, Color.Gray.PackedValue));
            Add(new Line(WIDTH, 0, 1, HEIGHT, Color.Gray.PackedValue));

            #endregion

            var initX = 10;
            var initY = 10;

            // Search Field
            Add(_searchTextBox = new SearchTextBoxControl(10, initY));

            initX += _searchTextBox.Width + 10;

            // Import SOS
            Add(_importSOSButton = new NiceButton(initX, initY, 80, 25, ButtonAction.Activate, ResGumps.SOSMarkerImport)
            {
                ButtonParameter = (int)ButtonsOption.IMPORT_SOS,
                IsSelectable = false,
                TextLabel =
                {
                    Hue = 0x33
                }
            });

            initX += _importSOSButton.Width + 10;

            // Import Treasure Map
            Add(_importTMapButton = new NiceButton(initX, initY, 80, 25, ButtonAction.Activate, ResGumps.TMapMarkerImport)
            {
                ButtonParameter = (int)ButtonsOption.IMPORT_TMAP,
                IsSelectable = false,
                TextLabel =
                {
                    Hue = 0x33
                }
            });

            Add(new Line(0, initY + 30, WIDTH, 1, Color.Gray.PackedValue));

            initY += 40;

            #region Legend

            Add(new Label(ResGumps.MarkerIcon, true, HUE_FONT, 185, 255, FontStyle.BlackBorder) { X = 5, Y = initY });
            Add(new Label(ResGumps.MarkerName, true, HUE_FONT, 185, 255, FontStyle.BlackBorder) { X = 50, Y = initY });
            Add(new Label(ResGumps.MarkerX, true, HUE_FONT, 35, 255, FontStyle.BlackBorder) { X = 315, Y = initY });
            Add(new Label(ResGumps.MarkerY, true, HUE_FONT, 35, 255, FontStyle.BlackBorder) { X = 380, Y = initY });
            Add(new Label(ResGumps.MarkerColor, true, HUE_FONT, 35, 255, FontStyle.BlackBorder) { X = 420, Y = initY });
            Add(new Label(ResGumps.Edit, true, HUE_FONT, 35, 255, FontStyle.BlackBorder) { X = 475, Y = initY });
            Add(new Label(ResGumps.Remove, true, HUE_FONT, 40, 255, FontStyle.BlackBorder) { X = 505, Y = initY });
            Add(new Label(ResGumps.MarkerGoTo, true, HUE_FONT, 40, 255, FontStyle.BlackBorder) { X = 550, Y = initY });

            #endregion

            Add(new Line(0, initY + 20, WIDTH, 1, Color.Gray.PackedValue));

            Add(_scrollArea = new ScrollArea(10, 80, WIDTH - 20, 370, true));

            DrawArea(_markerFiles[_categoryId].IsEditable);

            initX = 0;

            foreach (var file in _markerFiles)
            {
                var b = new NiceButton(button_width * initX, HEIGHT - 40, button_width, 40, ButtonAction.Activate, file.Name, MARKERS_CATEGORY_GROUP_INDEX)
                {
                    ButtonParameter = initX,
                    IsSelectable = true,
                };

                b.SetTooltip(file.Name);

                if (initX == 0)
                {
                    b.IsSelected = true;
                }

                Add(b);

                Add(new Line(b.X, b.Y, 1, b.Height, Color.Gray.PackedValue));

                initX++;
            }

            Add(new Line(0, HEIGHT - 40, WIDTH, 1, Color.Gray.PackedValue));

            SetInScreen();
        }

        private void DrawArea(bool isEditable)
        {
            _scrollArea.Clear();

            var idx = 0;

            foreach (var marker in _markers)
            {
                if (!string.IsNullOrWhiteSpace(_searchText) && marker.Name.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var newElement = new MarkerManagerControl(_markers, marker, idx * 25, idx, isEditable);

                newElement.RemoveMarkerEvent += MarkerRemoveEventHandler;
                newElement.EditMarkerEvent += MarkerEditEventHandler;

                _scrollArea.Add(newElement);

                ++idx;
            }
        }

        private void MarkerRemoveEventHandler(object sender, EventArgs e)
        {
            if (sender is int idx)
            {
                _markers.RemoveAt(idx);

                //Redraw List
                DrawArea(_markerFiles[_categoryId].IsEditable);

                //Mark list as Modified
                _isMarkerListModified = true;
            }
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case (int)ButtonsOption.SEARCH_BTN:
                {
                    if (_searchText.Equals(_searchTextBox.SearchText))
                    {
                        return;
                    }

                    _searchText = _searchTextBox.SearchText;
                    break;
                }

                case (int)ButtonsOption.CLEAR_SEARCH_BTN:
                {
                    _searchTextBox.ClearText();
                    _searchText = "";
                    break;
                }

                case (int)ButtonsOption.IMPORT_SOS:
                {
                    BeginTargetSOS();
                    break;
                }

                case (int)ButtonsOption.IMPORT_TMAP:
                {
                    BeginTargetTMap();
                    break;
                }

                default:
                {
                    _categoryId = buttonID;
                    _markers = _markerFiles[buttonID].Markers;
                    break;
                }
            }

            DrawArea(_markerFiles[_categoryId].IsEditable);
        }

        public override void OnKeyboardReturn(int textID, string text)
        {
            if (_searchText.Equals(_searchTextBox.SearchText))
            {
                return;
            }

            _searchText = _searchTextBox.SearchText;

            DrawArea(_markerFiles[_categoryId].IsEditable);
        }

        private void MarkerEditEventHandler(object sender, EventArgs e)
        {
            _isMarkerListModified = true;
        }

        #region SOS + TMap Markers

        static MarkersManagerGump()
        {
            UIManager.OnAdded += HandleSOSGump;
            UIManager.OnAdded += HandleTMapGump;
        }

        #region SOS

        private static Item _sosItem;
        private static int _sosAttempts;

        private static void SendSOSFailedMessage()
        {
            GameActions.Print(ResGumps.SOSMarkerFailed);
        }

        private static void SendSOSUpdatedMessage()
        {
            GameActions.Print(ResGumps.SOSMarkerUpdated);
        }

        private static void SendSOSAddedMessage()
        {
            GameActions.Print(ResGumps.SOSMarkerAdded);
        }

        public static void BeginTargetSOS()
        {
            _sosItem = null;
            _sosAttempts = 0;

            GameActions.Print(ResGumps.SOSMarkerTarget);

            TargetManager.SetLocalTargeting(TargetType.Neutral, OnTargetSOS);
        }

        private static void OnTargetSOS(LastTargetInfo target)
        {
            if (target.IsEntity)
            {
                var obj = World.Get(target.Serial) as Item;

                if (obj?.Name?.EndsWith("SOS") == true)
                {
                    _sosItem = obj;

                    NetClient.Socket.Send_DoubleClick(obj.Serial);

                    return;
                }
            }

            SendSOSFailedMessage();
        }

        private static void HandleSOSGump(Gump gump)
        {
            if (_sosItem == null)
            {
                return;
            }

            var userFile = _markerFiles.Where(f => f.Name == USER_MARKERS_FILE).FirstOrDefault();

            if (userFile == null)
            {
                _sosItem = null;
                _sosAttempts = 0;

                SendSOSFailedMessage();

                return;
            }

            if (!gump.IsFromServer)
            {
                return;
            }

            Match match = null;

            foreach (var c in gump.Children)
            {
                if (c is HtmlControl h)
                {
                    match = Regex.Match(h.Text, @"\d+[o|°]\s?\d+'[N|S],\s+\d+[o|°]\s?\d+'[E|W]");

                    if (match.Success)
                    {
                        break;
                    }
                }
            }

            if (match?.Success != true)
            {
                if (++_sosAttempts >= 10)
                {
                    _sosItem = null;
                    _sosAttempts = 0;

                    SendSOSFailedMessage();
                }

                return;
            }

            try
            {
                var manager = UIManager.GetGump<MarkersManagerGump>();

                var markerX = -1;
                var markerY = -1;

                ConvertCoords(match.Value, ref markerX, ref markerY);

                var cmp = StringComparison.OrdinalIgnoreCase;

                if (manager != null)
                {
                    if (manager._markers.Exists(m => m.MarkerIconName.IndexOf("SOS", cmp) >= 0 && m.MapId < 0 && m.X == markerX && m.Y == markerY))
                    {
                        SendSOSUpdatedMessage();
                        return;
                    }
                }
                else
                {
                    if (userFile.Markers.Exists(m => m.MarkerIconName.IndexOf("SOS", cmp) >= 0 && m.MapId < 0 && m.X == markerX && m.Y == markerY))
                    {
                        SendSOSUpdatedMessage();
                        return;
                    }
                }

                WMapMarker marker = new()
                {
                    X = markerX,
                    Y = markerY,
                    MapId = -1,
                    Name = _sosItem.Name,
                    MarkerIconName = "SOS",
                    Color = Color.Green,
                    ColorName = "green",
                    ZoomIndex = 3
                };

                if (MarkerIcons.TryGetValue(marker.MarkerIconName, out Texture2D value))
                {
                    marker.MarkerIcon = value;
                }

                SendSOSAddedMessage();

                if (manager != null)
                {
                    manager._markers.Add(marker);

                    manager._isMarkerListModified = true;

                    manager.DrawArea(userFile.IsEditable);
                }
                else
                {
                    userFile.Markers.Add(marker);

                    File.WriteAllLines(userFile.FullPath, userFile.Markers.Select(m => $"{m.X},{m.Y},{m.MapId},{m.Name},{m.MarkerIconName},{m.ColorName},{m.ZoomIndex}"));
                }

                var wmGump = UIManager.GetGump<WorldMapGump>();

                if (wmGump?.MapIndex is 0 or 1)
                {
                    wmGump.GoToMarker(marker.X, marker.Y, false);
                }
            }
            finally
            {
                gump.InvokeMouseCloseGumpWithRClick();

                BeginTargetSOS();
            }
        }

        #endregion

        #region TMap

        private static Item _tmapItem;

        private static void SendTMapFailedMessage()
        {
            GameActions.Print(ResGumps.TMapMarkerFailed);
        }

        private static void SendTMapUpdatedMessage()
        {
            GameActions.Print(ResGumps.TMapMarkerUpdated);
        }

        private static void SendTMapAddedMessage()
        {
            GameActions.Print(ResGumps.TMapMarkerAdded);
        }

        public static void BeginTargetTMap()
        {
            _tmapItem = null;

            GameActions.Print(ResGumps.TMapMarkerTarget);

            TargetManager.SetLocalTargeting(TargetType.Neutral, OnTargetTMap);
        }

        private static void OnTargetTMap(LastTargetInfo target)
        {
            if (target.IsEntity)
            {
                var obj = World.Get(target.Serial) as Item;

                var cmp = StringComparison.OrdinalIgnoreCase;

                if (obj?.Name?.IndexOf("treasure map", cmp) >= 0 && obj.Name.IndexOf("tattered", cmp) < 0)
                {
                    _tmapItem = obj;

                    NetClient.Socket.Send_DoubleClick(obj.Serial);

                    return;
                }
            }

            SendTMapFailedMessage();
        }

        private static void HandleTMapGump(Gump gump)
        {
            if (_tmapItem == null)
            {
                return;
            }

            var userFile = _markerFiles.Where(f => f.Name == USER_MARKERS_FILE).FirstOrDefault();

            if (userFile == null)
            {
                _tmapItem = null;

                SendTMapFailedMessage();

                return;
            }

            if (gump is not MapGump mapGump)
            {
                return;
            }

            Point? pin = null;

            foreach (var c in mapGump.Children)
            {
                if (c is MapGump.PinControl p)
                {
                    pin = new(p.InitX, p.InitY);
                    break;
                }
            }

            if (pin == null)
            {
                static void redirect(int mx, int my, MapGump.PinControl mp)
                {
                    HandleTMapGump(mp.Parent as Gump);
                }

                mapGump.OnPinAdded -= redirect;
                mapGump.OnPinAdded += redirect;

                return;
            }

            try
            {
                var manager = UIManager.GetGump<MarkersManagerGump>();

                var markerX = pin.Value.X;
                var markerY = pin.Value.Y;

                var cmp = StringComparison.OrdinalIgnoreCase;

                if (manager != null)
                {
                    if (manager._markers.Exists(m => m.MarkerIconName.IndexOf("TMAP", cmp) >= 0 && m.MapId == mapGump.MapId && m.X == markerX && m.Y == markerY))
                    {
                        SendTMapUpdatedMessage();
                        return;
                    }
                }
                else
                {
                    if (userFile.Markers.Exists(m => m.MarkerIconName.IndexOf("TMAP", cmp) >= 0 && m.MapId == mapGump.MapId && m.X == markerX && m.Y == markerY))
                    {
                        SendTMapUpdatedMessage();
                        return;
                    }
                }

                WMapMarker marker = new()
                {
                    X = markerX,
                    Y = markerY,
                    MapId = mapGump.MapId,
                    Name = _tmapItem.Name,
                    MarkerIconName = "TMAP",
                    Color = Color.Yellow,
                    ColorName = "yellow",
                    ZoomIndex = 3
                };

                if (MarkerIcons.TryGetValue(marker.MarkerIconName, out Texture2D value))
                {
                    marker.MarkerIcon = value;
                }

                SendTMapAddedMessage();

                if (manager != null)
                {
                    manager._markers.Add(marker);

                    manager._isMarkerListModified = true;

                    manager.DrawArea(userFile.IsEditable);
                }
                else
                {
                    userFile.Markers.Add(marker);

                    File.WriteAllLines(userFile.FullPath, userFile.Markers.Select(m => $"{m.X},{m.Y},{m.MapId},{m.Name},{m.MarkerIconName},{m.ColorName},{m.ZoomIndex}"));
                }

                var wmGump = UIManager.GetGump<WorldMapGump>();

                if (wmGump?.MapIndex == mapGump.MapId)
                {
                    wmGump.GoToMarker(marker.X, marker.Y, false);
                }
            }
            finally
            {
                gump.InvokeMouseCloseGumpWithRClick();

                BeginTargetTMap();
            }
        }

        #endregion

        #endregion

        public override void Dispose()
        {
            if (_isMarkerListModified)
            {
                File.WriteAllLines(_userMarkersFilePath, _markers.Select(m => $"{m.X},{m.Y},{m.MapId},{m.Name},{m.MarkerIconName},{m.ColorName},{m.ZoomIndex}"));

                _isMarkerListModified = false;

                ReloadUserMarkers();
            }

            base.Dispose();
        }

        internal class DrawTexture : Control
        {
            public Texture2D Texture;

            public DrawTexture(Texture2D texture)
            {
                Texture = texture;
                Width = Height = 15;
            }

            public override bool Draw(UltimaBatcher2D batcher, int x, int y)
            {
                var hueVector = ShaderHueTranslator.GetHueVector(0);

                batcher.Draw(Texture, new Rectangle(x, y + 7, Width, Height), hueVector);

                return true;
            }
        }

        private sealed class MarkerManagerControl : Control
        {
            private readonly List<WMapMarker> _markers;

            private readonly WMapMarker _marker;
            private readonly int _y;
            private readonly int _idx;
            private readonly bool _isEditable;

            private Label _labelName;
            private Label _labelX;
            private Label _labelY;
            private Label _labelColor;

            private DrawTexture _iconTexture;

            public event EventHandler RemoveMarkerEvent;
            public event EventHandler EditMarkerEvent;

            private enum ButtonsOption
            {
                EDIT_MARKER_BTN,
                REMOVE_MARKER_BTN,
                GOTO_MARKER_BTN
            }

            public MarkerManagerControl(List<WMapMarker> markers, WMapMarker marker, int y, int idx, bool isEditable)
            {
                CanMove = true;

                _markers = markers;
                _idx = idx;
                _marker = marker;
                _y = y;
                _isEditable = isEditable;

                DrawData();
            }

            private void DrawData()
            {
                if (_marker.MarkerIcon != null)
                {
                    Add(_iconTexture = new DrawTexture(_marker.MarkerIcon) { X = 0, Y = _y - 5 });
                }

                Add(_labelName = new Label($"{_marker.Name}", true, HUE_FONT, 280) { X = 30, Y = _y });

                Add(_labelX = new Label($"{_marker.X}", true, HUE_FONT, 35) { X = 305, Y = _y });
                Add(_labelY = new Label($"{_marker.Y}", true, HUE_FONT, 35) { X = 350, Y = _y });

                Add(_labelColor = new Label($"{_marker.ColorName}", true, HUE_FONT, 35) { X = 410, Y = _y });

                if (_isEditable)
                {
                    Add(new Button((int)ButtonsOption.EDIT_MARKER_BTN, 0xFAB, 0xFAC)
                    {
                        X = 470,
                        Y = _y,
                        ButtonAction = ButtonAction.Activate,
                    });

                    Add(new Button((int)ButtonsOption.REMOVE_MARKER_BTN, 0xFB1, 0xFB2)
                    {
                        X = 505,
                        Y = _y,
                        ButtonAction = ButtonAction.Activate,
                    });
                }

                Add(new Button((int)ButtonsOption.GOTO_MARKER_BTN, 0xFA5, 0xFA7)
                {
                    X = 540,
                    Y = _y,
                    ButtonAction = ButtonAction.Activate,
                });
            }

            private void OnEditEnd(object sender, EventArgs e)
            {
                if (sender is WMapMarker editedMarker)
                {
                    _labelName.Text = editedMarker.Name;
                    _labelColor.Text = editedMarker.ColorName;
                    _labelX.Text = editedMarker.X.ToString();
                    _labelY.Text = editedMarker.Y.ToString();

                    if (editedMarker.MarkerIcon != null)
                    {
                        _iconTexture?.Dispose();
                        _iconTexture = new DrawTexture(editedMarker.MarkerIcon);
                    }

                    EditMarkerEvent.Raise();
                }
            }

            public override void OnButtonClick(int buttonId)
            {
                switch (buttonId)
                {
                    case (int)ButtonsOption.EDIT_MARKER_BTN:
                    {
                        var existingGump = UIManager.GetGump<UserMarkersGump>();

                        existingGump?.Dispose();

                        var editUserMarkerGump = new UserMarkersGump(_marker.X, _marker.Y, _markers, _marker.ColorName, _marker.MarkerIconName, true, _idx);

                        editUserMarkerGump.EditEnd += OnEditEnd;

                        UIManager.Add(editUserMarkerGump);

                        break;
                    }

                    case (int)ButtonsOption.REMOVE_MARKER_BTN:
                    {
                        RemoveMarkerEvent.Raise(_idx);

                        break;
                    }

                    case (int)ButtonsOption.GOTO_MARKER_BTN:
                    {
                        var wmGump = UIManager.GetGump<WorldMapGump>();

                        wmGump?.GoToMarker(_marker.X, _marker.Y, false);

                        break;
                    }
                }
            }
        }

        private sealed class SearchTextBoxControl : Control
        {
            private readonly StbTextBox _textBox;

            public string SearchText => _textBox.Text;

            public SearchTextBoxControl(int x, int y)
            {
                AcceptMouseInput = true;
                AcceptKeyboardInput = true;

                Add(new Label(ResGumps.MarkerSearch, true, HUE_FONT, 50, 1)
                {
                    X = x,
                    Y = y + 5
                });

                Add(new ResizePic(0x0BB8)
                {
                    X = x + 50,
                    Y = y,
                    Width = 200,
                    Height = 25
                });

                Add(_textBox = new StbTextBox(0xFF, 30, 200, true, FontStyle.BlackBorder | FontStyle.Fixed)
                {
                    X = x + 53,
                    Y = y + 3,
                    Width = 200,
                    Height = 25
                });

                Add(new Button((int)ButtonsOption.SEARCH_BTN, 0xFB7, 0xFB9)
                {
                    X = x + 250,
                    Y = y + 1,
                    ButtonAction = ButtonAction.Activate,
                });

                Add(new Button((int)ButtonsOption.CLEAR_SEARCH_BTN, 0xFB1, 0xFB2)
                {
                    X = x + 285,
                    Y = y + 1,
                    ButtonAction = ButtonAction.Activate,
                });

                Width = Children.Max(c => c.X + c.Width);
                Height = Children.Max(c => c.Y + c.Height);
            }

            public void ClearText()
            {
                _textBox.SetText("");
            }
        }
    }
}
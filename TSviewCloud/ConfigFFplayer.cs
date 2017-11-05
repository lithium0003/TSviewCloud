using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace TSviewCloudConfig
{
    class ConfigFFplayer
    {
        public static FFmoduleKeybindsClass FFmoduleKeybinds = new FFmoduleKeybindsClass() {
            { ffmodule.FFplayerKeymapFunction.FuncPlayExit,         new FFmoduleKeysClass{System.Windows.Forms.Keys.Escape } },
            { ffmodule.FFplayerKeymapFunction.FuncSeekPlus10sec,    new FFmoduleKeysClass{System.Windows.Forms.Keys.Right } },
            { ffmodule.FFplayerKeymapFunction.FuncSeekMinus10sec,   new FFmoduleKeysClass{System.Windows.Forms.Keys.Left } },
            { ffmodule.FFplayerKeymapFunction.FuncSeekPlus60sec,    new FFmoduleKeysClass{System.Windows.Forms.Keys.Up} },
            { ffmodule.FFplayerKeymapFunction.FuncSeekMinus60sec,   new FFmoduleKeysClass{System.Windows.Forms.Keys.Down} },
            { ffmodule.FFplayerKeymapFunction.FuncVolumeUp,         new FFmoduleKeysClass{System.Windows.Forms.Keys.Insert} },
            { ffmodule.FFplayerKeymapFunction.FuncVolumeDown,       new FFmoduleKeysClass{System.Windows.Forms.Keys.Delete} },
            { ffmodule.FFplayerKeymapFunction.FuncToggleFullscreen, new FFmoduleKeysClass{System.Windows.Forms.Keys.F} },
            { ffmodule.FFplayerKeymapFunction.FuncToggleDisplay,    new FFmoduleKeysClass{System.Windows.Forms.Keys.D} },
            { ffmodule.FFplayerKeymapFunction.FuncToggleMute,       new FFmoduleKeysClass{System.Windows.Forms.Keys.M} },
            { ffmodule.FFplayerKeymapFunction.FuncCycleChannel,     new FFmoduleKeysClass{System.Windows.Forms.Keys.C} },
            { ffmodule.FFplayerKeymapFunction.FuncCycleAudio,       new FFmoduleKeysClass{System.Windows.Forms.Keys.A} },
            { ffmodule.FFplayerKeymapFunction.FuncCycleSubtitle,    new FFmoduleKeysClass{System.Windows.Forms.Keys.T} },
            { ffmodule.FFplayerKeymapFunction.FuncForwardChapter,   new FFmoduleKeysClass{System.Windows.Forms.Keys.PageUp} },
            { ffmodule.FFplayerKeymapFunction.FuncRewindChapter,    new FFmoduleKeysClass{System.Windows.Forms.Keys.PageDown} },
            { ffmodule.FFplayerKeymapFunction.FuncTogglePause,      new FFmoduleKeysClass{System.Windows.Forms.Keys.P} },
            { ffmodule.FFplayerKeymapFunction.FuncResizeOriginal,   new FFmoduleKeysClass{System.Windows.Forms.Keys.D0} },
            { ffmodule.FFplayerKeymapFunction.FuncSrcVolumeUp,      new FFmoduleKeysClass{System.Windows.Forms.Keys.F1} },
            { ffmodule.FFplayerKeymapFunction.FuncSrcVolumeDown,    new FFmoduleKeysClass{System.Windows.Forms.Keys.F2} },
            { ffmodule.FFplayerKeymapFunction.FuncSrcAutoVolume,    new FFmoduleKeysClass{System.Windows.Forms.Keys.F4} },
        };
        public static string FontFilePath = "ipaexg.ttf";
        public static int FontPtSize = 32;
        public static bool AutoResize = true;

        public static bool fullscreen = false;
        public static bool display = false;
        public static double volume = 50;
        public static bool mute = false;
        public static int width = 0;
        public static int hight = 0;
        public static int x = 0;
        public static int y = 0;

    }

    [DataContract]
    class SavedataFFplayer
    {
        [DataMember]
        public FFmoduleKeybindsClass FFmoduleKeybinds;
        [DataMember]
        public string FontFilePath;
        [DataMember]
        public int FontPtSize;
        [DataMember]
        public bool AutoResize;
        [DataMember]
        public bool fullscreen;
        [DataMember]
        public bool display;
        [DataMember]
        public double volume;
        [DataMember]
        public bool mute;
        [DataMember]
        public int width;
        [DataMember]
        public int hight;
        [DataMember]
        public int x;
        [DataMember]
        public int y;
    }

    [CollectionDataContract
    (Name = "FFmoduleKeyBinds",
    ItemName = "entry",
    KeyName = "command",
    ValueName = "keys")]
    public class FFmoduleKeybindsClass : Dictionary<ffmodule.FFplayerKeymapFunction, FFmoduleKeysClass> { }

    [CollectionDataContract(Name = "bindkeys")]
    public class FFmoduleKeysClass : Collection<System.Windows.Forms.Keys> { }
}

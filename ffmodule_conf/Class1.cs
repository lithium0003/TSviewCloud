using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ffmodule
{
    public enum FFplayerKeymapFunction
    {
        FuncPlayExit,
		FuncSeekPlus10sec,
		FuncSeekMinus10sec,
		FuncSeekPlus60sec,
		FuncSeekMinus60sec,
		FuncVolumeUp,
		FuncVolumeDown,
		FuncToggleFullscreen,
		FuncToggleDisplay,
		FuncToggleMute,
		FuncCycleChannel,
		FuncCycleAudio,
		FuncCycleSubtitle,
		FuncForwardChapter,
		FuncRewindChapter,
		FuncTogglePause,
		FuncResizeOriginal,
		FuncSrcVolumeUp,
		FuncSrcVolumeDown,
		FuncSrcAutoVolume,
	};

    public delegate System.Drawing.Bitmap GetImageDelegate(dynamic sender);
}

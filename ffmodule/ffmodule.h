#pragma once

#include <vcclr.h>
#include <msclr\marshal.h>

extern "C" {
#include <libavcodec/avcodec.h>
#include <libavformat/avformat.h>
#include <libswscale/swscale.h>
#include <libavutil/avutil.h>
#include <libavutil/imgutils.h>
#include <libavutil/opt.h>
#include <libavutil/avstring.h>
#include <libavutil/time.h>
#include <libswresample/swresample.h>
#include <libavfilter/avfilter.h>
#include <libavfilter/buffersink.h>
#include <libavfilter/buffersrc.h>
}

#pragma comment(lib, "avcodec.lib")
#pragma comment(lib, "avformat.lib")
#pragma comment(lib, "swscale.lib")
#pragma comment(lib, "avutil.lib")
#pragma comment(lib, "swresample.lib")
#pragma comment(lib, "avfilter.lib")

#include <SDL.h>
#include <SDL_thread.h>

#pragma comment(lib, "SDL2.lib")

#include <SDL_ttf.h>

#pragma comment(lib, "SDL2_ttf.lib")

#include <assert.h>
#include <string>
#include <map>
#include <queue>
#include <functional>
#include <list>
#include <fstream>

#include <stdlib.h>
#include <inttypes.h>

#define SDL_AUDIO_BUFFER_SIZE 8 * 1024
#define MAX_AUDIO_FRAME_SIZE 192000

#define FF_REFRESH_EVENT (SDL_USEREVENT)
#define FF_INTERNAL_REFRESH_EVENT (SDL_USEREVENT + 1)
#define FF_QUIT_EVENT (SDL_USEREVENT + 2)

#define VIDEO_PICTURE_QUEUE_SIZE 40

#define MAX_AUDIOQ_SIZE (8 * 1024 * 1024)
#define MAX_VIDEOQ_SIZE (64 * 1024 * 1024)

#define AV_SYNC_THRESHOLD 0.005
#define AV_NOSYNC_THRESHOLD 8.0

#define SAMPLE_CORRECTION_PERCENT_MAX 10
#define AUDIO_DIFF_AVG_NB 20

#define DEFAULT_AV_SYNC_TYPE AV_SYNC_AUDIO_MASTER

#define CURSOR_HIDE_DELAY 5000000

namespace ffmodule {
	using namespace System;
	using namespace System::Runtime::InteropServices;
	using namespace System::IO;

	enum AV_SYNC_TYPE {
		AV_SYNC_AUDIO_MASTER,
		AV_SYNC_VIDEO_MASTER,
		AV_SYNC_EXTERNAL_MASTER,
	};

	class _FFplayer;

	class PacketQueue
	{
	private:
		_FFplayer *parent;
		const std::shared_ptr<SDL_mutex> mutex;
		const std::shared_ptr<SDL_cond> cond;
	public:
		AVPacketList *first_pkt, *last_pkt;
		int nb_packets;
		int size;

		PacketQueue(_FFplayer *parent);
		~PacketQueue();
		void AbortQueue();
		int putEOF();
		int put(AVPacket *pkt);
		int get(AVPacket *pkt, int block);
		void flush();
		void clear();
	};

	class VideoPicture
	{
	public:
		AVFrame bmp;
		int width, height; /* source height & width */
		bool allocated;
		double pts;
		int64_t serial;

		VideoPicture() { }
		~VideoPicture();
		bool Allocate(int width, int height);
		void Free();
	};

	class SubtitlePicture
	{
	public:
		int type;
		std::unique_ptr<std::shared_ptr<AVSubtitleRect>[]> subrects;
		int numrects;
		uint32_t start_display_time;
		uint32_t end_display_time;
		int subw;
		int subh;
		double pts;
		int64_t serial;
	};

	class SubtitlePictureQueue
	{
	private:
		_FFplayer *parent;
		const std::shared_ptr<SDL_mutex> mutex;
		const std::shared_ptr<SDL_cond> cond;
		std::queue<std::shared_ptr<SubtitlePicture>> queue;
	public:

		SubtitlePictureQueue(_FFplayer *parent);
		~SubtitlePictureQueue();
		void clear();
		void put(std::shared_ptr<SubtitlePicture> Pic);
		int peek(std::shared_ptr<SubtitlePicture>& Pic);
		int get(std::shared_ptr<SubtitlePicture> &Pic);
	};

	class _Stream {
	private:
		const int buffersize = 4 * 1024 + FF_INPUT_BUFFER_PADDING_SIZE;
		gcroot<System::IO::Stream^> stream;
		std::shared_ptr<uint8_t> buffer;
		gcroot<System::Threading::CancellationToken^> ct;
	public:
		_Stream(System::IO::Stream^ stream, System::Threading::CancellationToken^ ct);
		~_Stream();

		uint8_t *getbuffer() {
			return buffer.get();
		}

		int getbuffersize() {
			return buffersize;
		}

		void Quit() {
			stream->ReadTimeout = 0;
		}

		uint64_t StreamLength()
		{
			if (static_cast<Stream^>(stream) == nullptr) return -1;
			return stream->Length;
		}

		static int read_packet(void *opaque, uint8_t *buf, int buf_size);
		static int64_t seek(void *opaque, int64_t offset, int whence);
	};


	class SDLScreen {
	private:
		int width;
		int height;
		int srcwidth;
		int srcheight;
		bool show;
		Uint32 WindowID;
		uint64_t PendingChangeT;
		uint64_t FinishT;
	public:
		std::shared_ptr<SDL_Window> window;
		std::shared_ptr<SDL_Renderer> renderer;
		std::shared_ptr<SDL_Texture> texture;
		std::unique_ptr<std::shared_ptr<SDL_Texture>[]> subtitle;
		SDL_Texture *statictexture; //need not to free
		int subtitlelen;
		uint64_t subserial;
		int subwidth;

		bool fullscreen; 
		std::string title;

		SDLScreen();
		SDLScreen(int width, int height);

		bool SetScreenSize();

		bool SetScreenSize(int width, int height);
		bool SetScreenSize(int width, int height, int srcwidth, int srcheight);
		void SetPosition(int x, int y);
		void GetPosition(int * x, int * y);
		bool IsFinished(uint64_t t);
		bool SetFullScreen(bool fullscreen);

		SDL_Texture* CreateStaticTexture(SDL_Surface* surface);

		bool RequestSrcSize(int width, int height, uint64_t t);

		int GetWidth();
		int GetHight();
		UInt32 GetWindowID();
		void ShowWindow();
	};

	class _FFplayer {
	private:
		char            filename[1024];
		bool            IsPlay;

		SDL_threadID    mainthread;

		std::shared_ptr<TTF_Font> font;
		int64_t         overlay_remove_time;
		std::string     overlay_text;
		bool            cursor_hidden;
		int64_t         cursor_last_shown;

		std::shared_ptr<AVIOContext>     IOCtx;
		std::shared_ptr<AVFormatContext> pFormatCtx;
		int             videoStream, audioStream;
		int             subtitleStream;

		std::shared_ptr<_Stream> inputstream;

		bool            pause;
		double          startskip_internal;

		AV_SYNC_TYPE    av_sync_type;
		double          external_clock; /* external clock base */

		double			pos_ratio;
		bool            left_seek;

		bool            seek_req;
		int             seek_flags;
		int64_t         seek_pos;
		int64_t         seek_rel;
		double          prev_pos;

		bool            seek_req_backorder;
		int             seek_flags_backorder;
		int64_t         seek_pos_backorder;
		int64_t         seek_rel_backorder;

		SDL_AudioDeviceID audio_deviceID;

		AVStream        *audio_st;
		std::shared_ptr<AVCodecContext> audio_ctx;
		PacketQueue     audioq;
		uint8_t         audio_buf[MAX_AUDIO_FRAME_SIZE * 10];
		unsigned int    audio_buf_size;
		unsigned int    audio_buf_index;
		double          audio_diff_cum; /* used for AV difference average computation */
		double          audio_diff_avg_coef;
		double          audio_diff_threshold;
		int             audio_diff_avg_count;
		std::shared_ptr<SwrContext>     swr_ctx;
		int             audio_out_sample_rate;
		int             audio_out_channels;
		double          audio_clock;
		double          audio_clock_start;
		enum audio_eof_enum {
			playing,
			input_eof,
			output_eof,
			eof,
		}				audio_eof;
		bool            audio_only;
		std::shared_ptr<AVFilterGraph> agraph;
		AVFilterContext *afilt_out, *afilt_in;
		int             audio_volume_dB;
		bool            audio_volume_auto;
		int64_t         audio_serial;

		typedef struct AudioParams {
			int freq;
			int channels;
			int64_t channel_layout;
			enum AVSampleFormat fmt;
			int frame_size;
			int bytes_per_sec;
			int audio_volume_dB;
			bool audio_volume_auto;
			int64_t serial;
		} AudioParams;
		AudioParams audio_filter_src;

		bool            audio_pause;
		double          audio_callback_time;

		AVStream        *subtitle_st;
		std::shared_ptr<AVCodecContext> subtitle_ctx;
		PacketQueue     subtitleq;
		SubtitlePictureQueue subpictq;
		int64_t         subpictq_active_serial;
		double          frame_timer;
		double          frame_last_pts;
		double	        frame_last_delay;
		long            force_draw;
		long            remove_refresh;
		double          video_delay_to_audio;

		AVStream        *video_st;
		std::shared_ptr<AVCodecContext> video_ctx;
		PacketQueue     videoq;
		std::shared_ptr<SwsContext>     sws_ctx;
		double          video_clock; // pts of last decoded frame / predicted pts of next decoded frame
		double          video_current_pts; ///<current displayed pts (different from video_clock if frame fifos are used)
		int64_t         video_current_pts_time;  ///<time (av_gettime) at which we updated video_current_pts - used to have running video pts
		double          video_clock_start;
		bool            video_eof;
		bool            video_only;
		int             video_width;
		int             video_height;
		int             video_srcwidth;
		int             video_srcheight;
		AVRational      video_SAR;

		bool            deinterlace;

		VideoPicture    pictq[VIDEO_PICTURE_QUEUE_SIZE];
		int             pictq_size, pictq_rindex, pictq_windex;
		VideoPicture*   pictq_prev;
		int64_t         pictq_active_serial;
		bool            pict_seek_after;
		const std::shared_ptr<SDL_mutex> pictq_mutex;
		const std::shared_ptr<SDL_cond>  pictq_cond;

		std::shared_ptr<SDLScreen>       screen;
		const std::shared_ptr<SDL_mutex> screen_mutex;
		bool			IsFullscreen;

		SDL_Thread      *parse_tid;
		SDL_Thread      *video_tid;
		SDL_Thread      *subtitle_tid;

		class _FFplayerFuncs {
			_FFplayer	*parent;

			double incr;
			double frac;

			std::map<SDL_Keycode, int(_FFplayer::_FFplayerFuncs::*)(SDL_Event &evnt)> KeyUpFuncs;
			std::map<SDL_Keycode, int(_FFplayer::_FFplayerFuncs::*)(SDL_Event &evnt)> KeyDownFuncs;
		public:
			_FFplayerFuncs(_FFplayer *p) : parent(p) {}
			// -1: break false
			// 0: loop continue
			// 1: break true
			int VoidFunction(SDL_Event &evnt);
			int QuitTrueFunction(SDL_Event &evnt);
			int QuitFalseFunction(SDL_Event &evnt);
			int MouseMotionFunction(SDL_Event & evnt);
			int MouseButtonUpFunction(SDL_Event & evnt);
			int WindowFunction(SDL_Event & evnt);
			int KeyUpFunction(SDL_Event & evnt);
			int KeyDownFunction(SDL_Event & evnt);
			int RefreshFunction(SDL_Event & evnt);

			void RegisterKeyFunction(SDL_Keycode key, int(_FFplayer::_FFplayerFuncs::* keydownfnc)(SDL_Event &evnt), int(_FFplayer::_FFplayerFuncs::* keyupfnc)(SDL_Event &evnt));

			int FunctionToggleFullscreen(SDL_Event & evnt);
			int FunctionToggleDisplay(SDL_Event & evnt);

			int FunctionVolumeUp(SDL_Event & evnt);
			int FunctionVolumeDown(SDL_Event & evnt);
			int FunctionToggleMute(SDL_Event & evnt);

			int FunctionSeekPlus10sec(SDL_Event & evnt);
			int FunctionSeekMinus10sec(SDL_Event & evnt);
			int FunctionSeekPlus60sec(SDL_Event & evnt);
			int FunctionSeekMinus60sec(SDL_Event & evnt);
			int FunctionSeekIncDone(SDL_Event & evnt);

			int FunctionCycleChannel(SDL_Event & evnt);
			int FunctionCycleAudio(SDL_Event & evnt);
			int FunctionCycleSubtitle(SDL_Event & evnt);

			int FunctionForwardChapter(SDL_Event & evnt);
			int FunctionRewindChapter(SDL_Event & evnt);

			int FunctionTogglePause(SDL_Event & evnt);

			int FunctionResizeOriginal(SDL_Event & evnt);

			int FunctionSrcVolumeUp(SDL_Event & evnt);
			int FunctionSrcVolumeDown(SDL_Event & evnt);

			int FunctionToggleDynNormalizeVolume(SDL_Event & evnt);

			int invoke(int(_FFplayer::_FFplayerFuncs::*test)(SDL_Event &evnt), SDL_Event &evnt) {
				return (this->*test)(evnt);
			}
		};
		_FFplayerFuncs  funcs;

	public:
		typedef enum {
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
		} FFplayer_KeyCommand;

	private:
		const std::map<FFplayer_KeyCommand, SDL_Keycode> defaultkeymap = {
			{ FuncPlayExit,			SDLK_ESCAPE },
			{ FuncSeekPlus10sec,	SDLK_RIGHT },
			{ FuncSeekMinus10sec,	SDLK_LEFT },
			{ FuncSeekPlus60sec,	SDLK_UP },
			{ FuncSeekMinus60sec,	SDLK_DOWN },
			{ FuncVolumeUp,			SDLK_INSERT },
			{ FuncVolumeDown,		SDLK_DELETE },
			{ FuncToggleFullscreen, SDLK_f },
			{ FuncToggleDisplay,	SDLK_d },
			{ FuncToggleMute,		SDLK_m },
			{ FuncCycleChannel,		SDLK_c },
			{ FuncCycleAudio,		SDLK_a },
			{ FuncCycleSubtitle,	SDLK_t },
			{ FuncForwardChapter,	SDLK_PAGEUP },
			{ FuncRewindChapter,	SDLK_PAGEDOWN },
			{ FuncTogglePause,		SDLK_p },
			{ FuncResizeOriginal,	SDLK_0 },
			{ FuncSrcVolumeUp,		SDLK_F1 },
			{ FuncSrcVolumeDown,	SDLK_F2 },
			{ FuncSrcAutoVolume,	SDLK_F4 },
		};

		const std::map<FFplayer_KeyCommand, std::pair<int(_FFplayer::_FFplayerFuncs::*)(SDL_Event &evnt), int(_FFplayer::_FFplayerFuncs::*)(SDL_Event &evnt)>> keyfunctionlist = {
			{ FuncPlayExit,			{ &_FFplayer::_FFplayerFuncs::QuitTrueFunction,			&_FFplayer::_FFplayerFuncs::VoidFunction } },
			{ FuncSeekPlus10sec,	{ &_FFplayer::_FFplayerFuncs::FunctionSeekPlus10sec,	&_FFplayer::_FFplayerFuncs::FunctionSeekIncDone } },
			{ FuncSeekMinus10sec,	{ &_FFplayer::_FFplayerFuncs::FunctionSeekMinus10sec,	&_FFplayer::_FFplayerFuncs::FunctionSeekIncDone } },
			{ FuncSeekPlus60sec,	{ &_FFplayer::_FFplayerFuncs::FunctionSeekPlus60sec,	&_FFplayer::_FFplayerFuncs::FunctionSeekIncDone } },
			{ FuncSeekMinus60sec,	{ &_FFplayer::_FFplayerFuncs::FunctionSeekMinus60sec,	&_FFplayer::_FFplayerFuncs::FunctionSeekIncDone } },
			{ FuncVolumeUp,			{ &_FFplayer::_FFplayerFuncs::FunctionVolumeUp,			&_FFplayer::_FFplayerFuncs::VoidFunction } },
			{ FuncVolumeDown,		{ &_FFplayer::_FFplayerFuncs::FunctionVolumeDown,		&_FFplayer::_FFplayerFuncs::VoidFunction } },
			{ FuncToggleFullscreen,	{ &_FFplayer::_FFplayerFuncs::FunctionToggleFullscreen, &_FFplayer::_FFplayerFuncs::VoidFunction } },
			{ FuncToggleDisplay,	{ &_FFplayer::_FFplayerFuncs::FunctionToggleDisplay,	&_FFplayer::_FFplayerFuncs::VoidFunction } },
			{ FuncToggleMute,		{ &_FFplayer::_FFplayerFuncs::FunctionToggleMute,		&_FFplayer::_FFplayerFuncs::VoidFunction } },
			{ FuncCycleChannel,		{ &_FFplayer::_FFplayerFuncs::FunctionCycleChannel,		&_FFplayer::_FFplayerFuncs::VoidFunction } },
			{ FuncCycleAudio,		{ &_FFplayer::_FFplayerFuncs::FunctionCycleAudio,		&_FFplayer::_FFplayerFuncs::VoidFunction } },
			{ FuncCycleSubtitle,	{ &_FFplayer::_FFplayerFuncs::FunctionCycleSubtitle,	&_FFplayer::_FFplayerFuncs::VoidFunction } },
			{ FuncForwardChapter,	{ &_FFplayer::_FFplayerFuncs::FunctionForwardChapter,	&_FFplayer::_FFplayerFuncs::VoidFunction } },
			{ FuncRewindChapter,	{ &_FFplayer::_FFplayerFuncs::FunctionRewindChapter,	&_FFplayer::_FFplayerFuncs::VoidFunction } },
			{ FuncTogglePause,		{ &_FFplayer::_FFplayerFuncs::FunctionTogglePause,		&_FFplayer::_FFplayerFuncs::VoidFunction } },
			{ FuncResizeOriginal,	{ &_FFplayer::_FFplayerFuncs::FunctionResizeOriginal,	&_FFplayer::_FFplayerFuncs::VoidFunction } },
			{ FuncSrcVolumeUp,		{ &_FFplayer::_FFplayerFuncs::FunctionSrcVolumeUp,		&_FFplayer::_FFplayerFuncs::VoidFunction } },
			{ FuncSrcVolumeDown,	{ &_FFplayer::_FFplayerFuncs::FunctionSrcVolumeDown,	&_FFplayer::_FFplayerFuncs::VoidFunction } },
			{ FuncSrcAutoVolume,	{ &_FFplayer::_FFplayerFuncs::FunctionToggleDynNormalizeVolume,	&_FFplayer::_FFplayerFuncs::VoidFunction } },
		};
	public:
		std::multimap<FFplayer_KeyCommand, SDL_Keycode> KeyFunctions;

	public:
		volatile bool   quit;
		gcroot<System::Threading::CancellationToken^> ct;
		gcroot<System::Threading::CancellationTokenSource^> cts;
		double          audio_volume; // 100 = MAX
		bool            audio_mute;
		bool            display_on;
		std::string     fontfile;
		int             fontsize;
		double          duration;
		double          playtime;
		double          startskip;
		double          stopduration;
		int             screenwidth;
		int             screenheight;
		int             screenxpos;
		int             screenypos;
		bool            screenauto;

		bool GetFullscreenState() 
		{
			return (screen == NULL) ? false : screen->fullscreen;
		}
		bool GetFullscreen()
		{
			return IsFullscreen;
		}
		void SetFullscreen(bool value)
		{
			IsFullscreen = value;
			if (screen != NULL) ToggleFullscreen(value);
		}

		typedef  int(__stdcall *CallBackGetImageProc)(void **);
		CallBackGetImageProc getimagefunc;

		_FFplayer();
		~_FFplayer();
		bool Init();
		bool IsQuit();
		static int decode_interrupt_cb(void *ctx);
		static Uint32 sdl_refresh_timer_cb(Uint32 interval, void *opaque);
		static Uint32 sdl_internal_refresh_timer_cb(Uint32 interval, void *opaque);
		void schedule_refresh(int delay);
		double get_audio_clock();
		double get_video_clock();
		double get_external_clock();
		double get_master_clock();
		double get_master_clock_start();
		bool configure_audio_filters();
		void Finalize();
		int audio_decode_frame(uint8_t *audio_buf, int buf_size, double *pts_ptr);
		int synchronize_audio(short *samples, int samples_size, double pts);
		static void audio_callback(void *userdata, Uint8 *stream, int len);
		void destory_pictures();
		void destory_all_pictures();
		int queue_picture(AVFrame *pFrame, double pts);
		double synchronize_video(AVFrame *src_frame, double pts, double framerate);
		bool Configure_VideoFilter(AVFilterContext ** filt_in, AVFilterContext ** filt_out, AVFrame * frame, AVFilterGraph * graph);
		static int video_thread(void *arg);
		static int subtitle_thread(void * arg);
		bool SetScreenSize(int width, int height, int src_width, int src_height);
		int stream_component_open(int stream_index);
		void stream_component_close(int stream_index);
		static int decode_thread(void *arg);
		void stream_cycle_channel(int codec_type);
		void stream_seek(int64_t pos, int rel);
		void SeekExternal(double pos);
		void seek_chapter(int incr);
		void overlay_txt(VideoPicture *vp);
		double get_duration();
		void overlay_txt(double pts);
		std::shared_ptr<SDL_Surface> subtitle_ass(const char * text);
		void subtitle_display(double pts);
		void video_display(VideoPicture *vp);
		void audioonly_video_display();
		void set_clockduration(double pts);
		void Redraw();
		VideoPicture * next_picture_queue();
		void video_refresh_timer();
		void TogglePause();
		int Play(Stream^ input, const char *name);
		void ToggleFullscreen();
		void ToggleFullscreen(bool fullscreen);
		void ResizeOriginal();
		void EventOnVolumeChange();
		void EventOnSeek(double value, bool frac, bool pre);
		void EventOnSrcVolumeChange();
		bool EventLoop();
		void Quit();
	};
}
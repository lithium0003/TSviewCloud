// ffmodule.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"


#include "ffmodule.h"

extern AVPacket flush_pkt;
extern AVPacket eof_pkt;
extern AVPacket abort_pkt;

extern HANDLE hPlayEvent;
extern unsigned int PlayerCount;
extern HANDLE hLogMutex;
void EnterPlayer();
bool LeavePlayer();

namespace ffmodule {
	SDL_Keycode GetKeycode(System::Windows::Forms::Keys key);

	SDLScreen::SDLScreen()
	{
		SetScreenSize(640, 480, 0, 0);
		show = false;
	}
	SDLScreen::SDLScreen(int width, int height)
	{
		SetScreenSize(width, height, 0, 0);
		show = false;
	}

	bool SDLScreen::SetScreenSize()
	{
		return SetScreenSize(width, height, srcwidth, srcheight);
	}

	bool SDLScreen::SetScreenSize(int width, int height)
	{
		return SetScreenSize(width, height, srcwidth, srcheight);
	}

	bool SDLScreen::SetScreenSize(int width, int height, int srcwidth, int srcheight)
	{
		if (window.get() != NULL) {
			int x, y, w, h;
			SDL_GetWindowPosition(window.get(), &x, &y);
			SDL_GetWindowSize(window.get(), &w, &h);
			x += (w - width) / 2;
			y += (h - height) / 2;
			SDL_SetWindowPosition(window.get(), x, y);
			SDL_SetWindowSize(window.get(), width, height);
			SDL_SetWindowTitle(window.get(), title.c_str());
			SDL_SetWindowFullscreen(window.get(), (fullscreen ? SDL_WINDOW_FULLSCREEN_DESKTOP : 0));
			if (show) {
				SDL_ShowWindow(window.get());
				SDL_RaiseWindow(window.get());
			}
		}
		else {
			window = std::shared_ptr<SDL_Window>(SDL_CreateWindow(title.c_str(),
				SDL_WINDOWPOS_CENTERED,
				SDL_WINDOWPOS_CENTERED,
				width, height,
				(fullscreen? SDL_WINDOW_FULLSCREEN_DESKTOP : 0) |
				(show ? SDL_WINDOW_SHOWN: SDL_WINDOW_HIDDEN) |
				SDL_WINDOW_RESIZABLE
			), &SDL_DestroyWindow);
			if (window.get() == NULL) {
				return false;
			}
			WindowID = SDL_GetWindowID(window.get());

			SDL_SetHint(SDL_HINT_RENDER_SCALE_QUALITY, "linear");
			renderer = std::shared_ptr<SDL_Renderer>(SDL_CreateRenderer(
				window.get(), -1, SDL_RENDERER_ACCELERATED | SDL_RENDERER_TARGETTEXTURE
			), &SDL_DestroyRenderer);
			if (renderer.get() == NULL) {
				return false;
			}
		}
		texture = NULL;
		subtitle.reset(NULL);
		texture = std::shared_ptr<SDL_Texture>(
			SDL_CreateTexture(
				renderer.get(),
				SDL_PIXELFORMAT_YV12,
				SDL_TEXTUREACCESS_STREAMING,
				srcwidth, 
				srcheight),
			&SDL_DestroyTexture);
		subtitlelen = 0;
		subserial = 0;

		this->width = width;
		this->height = height;
		this->srcwidth = srcwidth;
		this->srcheight = srcheight;
		if (PendingChangeT) {
			FinishT = PendingChangeT;
			PendingChangeT = 0;
		}

		return true;
	}

	void SDLScreen::SetPosition(int x, int y)
	{
		SDL_SetWindowPosition(window.get(), x, y);
	}

	void SDLScreen::GetPosition(int *x, int *y)
	{
		SDL_GetWindowPosition(window.get(), x, y);
	}

	bool SDLScreen::IsFinished(uint64_t t)
	{
		return FinishT == t;
	}

	SDL_Texture* SDLScreen::CreateStaticTexture(SDL_Surface* surface)
	{
		statictexture = SDL_CreateTextureFromSurface(
				renderer.get(),
				surface
			);
		return statictexture;
	}

	bool SDLScreen::RequestSrcSize(int width, int height, uint64_t t)
	{
		if (PendingChangeT) return false;
		srcwidth = width;
		srcheight = height;
		PendingChangeT = t;
		return true;
	}

	bool SDLScreen::SetFullScreen(bool fullscreen)
	{
		if (fullscreen) {
			this->fullscreen = true;
			return SetScreenSize(width, height);
		}
		else {
			this->fullscreen = false;
			return SetScreenSize(width, height);
		}
	}

	int SDLScreen::GetWidth()
	{
		if (fullscreen) {
			int w;
			SDL_GetWindowSize(window.get(), &w, NULL);
			return w;
		}
		else
			return width;
	}

	int SDLScreen::GetHight()
	{
		if (fullscreen) {
			int h;
			SDL_GetWindowSize(window.get(), NULL, &h);
			return h;
		}
		else
			return height;
	}

	Uint32 SDLScreen::GetWindowID() 
	{
		return WindowID;
	}

	void SDLScreen::ShowWindow()
	{
		if (!show) {
			show = true;
			SDL_ShowWindow(window.get());
			SDL_RaiseWindow(window.get());
		}
	}

//////////////////////////////////////////////////////////////////////////////////////////////////////

	PacketQueue::PacketQueue(_FFplayer *parent) : mutex(SDL_CreateMutex(), SDL_DestroyMutex),
		cond(SDL_CreateCond(), SDL_DestroyCond)
	{
		this->parent = parent;
	}

	PacketQueue::~PacketQueue()
	{
		clear();
	}

	void PacketQueue::AbortQueue()
	{
		AVPacketList *pktabort;
		pktabort = (AVPacketList *)av_mallocz(sizeof(AVPacketList));
		if (!pktabort)
			return;
		pktabort->pkt = abort_pkt;
		pktabort->next = NULL;


		SDL_LockMutex(mutex.get());

		AVPacketList *pkt1;
		for (auto pkt = first_pkt; pkt != NULL; pkt = pkt1) {
			pkt1 = pkt->next;
			if ((pkt->pkt.data != flush_pkt.data) &&
				(pkt->pkt.data != eof_pkt.data) &&
				(pkt->pkt.data != abort_pkt.data)) {

				av_packet_unref(&pkt->pkt);
			}
			av_free(pkt);
		}
		last_pkt = NULL;
		first_pkt = NULL;
		nb_packets = 0;
		size = 0;

		first_pkt = pktabort;
		last_pkt = pktabort;
		nb_packets++;
		size += pktabort->pkt.size;
		SDL_CondSignal(cond.get());

		SDL_UnlockMutex(mutex.get());
		return;
	}

	int PacketQueue::putEOF()
	{
		AVPacketList *pkt1;

		pkt1 = (AVPacketList *)av_mallocz(sizeof(AVPacketList));
		if (!pkt1)
			return -1;
		pkt1->pkt = eof_pkt;
		pkt1->next = NULL;


		SDL_LockMutex(mutex.get());

		if (!last_pkt)
			first_pkt = pkt1;
		else
			last_pkt->next = pkt1;
		last_pkt = pkt1;
		nb_packets++;
		size += pkt1->pkt.size;
		SDL_CondSignal(cond.get());

		SDL_UnlockMutex(mutex.get());
		return 0;
	}

	int PacketQueue::put(AVPacket *pkt)
	{
		AVPacketList *pkt1;

		pkt1 = (AVPacketList *)av_mallocz(sizeof(AVPacketList));
		if (!pkt1)
			return -1;
		pkt1->pkt = *pkt;
		pkt1->next = NULL;


		SDL_LockMutex(mutex.get());

		if (!last_pkt)
			first_pkt = pkt1;
		else
			last_pkt->next = pkt1;
		last_pkt = pkt1;
		nb_packets++;
		size += pkt1->pkt.size;
		SDL_CondSignal(cond.get());

		SDL_UnlockMutex(mutex.get());
		return 0;
	}

	int PacketQueue::get(AVPacket *pkt, int block)
	{
		AVPacketList *pkt1;
		int ret;

		SDL_LockMutex(mutex.get());

		for (;;) {

			if (parent->IsQuit()) {
				ret = -1;
				break;
			}

			pkt1 = first_pkt;
			if (pkt1) {
				first_pkt = pkt1->next;
				if (!first_pkt)
					last_pkt = NULL;
				nb_packets--;
				size -= pkt1->pkt.size;
				*pkt = pkt1->pkt;
				av_free(pkt1);
				ret = 1;
				break;
			}
			else if (!block) {
				ret = 0;
				break;
			}
			else {
				SDL_CondWait(cond.get(), mutex.get());
			}
		}
		SDL_UnlockMutex(mutex.get());
		return ret;
	}

	void PacketQueue::flush()
	{
		AVPacketList *pktflush;

		pktflush = (AVPacketList *)av_mallocz(sizeof(AVPacketList));
		if (!pktflush)
			return;
		pktflush->pkt = flush_pkt;
		pktflush->next = NULL;

		SDL_LockMutex(mutex.get());
		AVPacketList *pkt1;
		for (auto pkt = first_pkt; pkt != NULL; pkt = pkt1) {
			pkt1 = pkt->next;
			if ((pkt->pkt.data != flush_pkt.data) &&
				(pkt->pkt.data != eof_pkt.data) &&
				(pkt->pkt.data != abort_pkt.data)) {

				av_packet_unref(&pkt->pkt);
			}
			av_free(pkt);
		}
		last_pkt = NULL;
		first_pkt = NULL;
		nb_packets = 0;
		size = 0;

		first_pkt = pktflush;
		last_pkt = pktflush;
		nb_packets++;
		size += pktflush->pkt.size;
		SDL_CondSignal(cond.get());

		SDL_UnlockMutex(mutex.get());
		return;
	}
	
	void PacketQueue::clear()
	{
		AVPacketList *pkt1;

		SDL_LockMutex(mutex.get());
		for (auto pkt = first_pkt; pkt != NULL; pkt = pkt1) {
			pkt1 = pkt->next;
			if ((pkt->pkt.data != flush_pkt.data) &&
				(pkt->pkt.data != eof_pkt.data) &&
				(pkt->pkt.data != abort_pkt.data)) {

				av_packet_unref(&pkt->pkt);
			}
			av_free(pkt);
		}
		last_pkt = NULL;
		first_pkt = NULL;
		nb_packets = 0;
		size = 0;
		SDL_UnlockMutex(mutex.get());
	}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////

	VideoPicture::~VideoPicture()
	{
		this->Free();
	}

	bool VideoPicture::Allocate(int width, int height)
	{
		Free();
		if (av_image_alloc(bmp.data, bmp.linesize, width, height, AV_PIX_FMT_YUV420P, 8) < 0) return false;
		this->width = width;
		this->height = height;
		this->allocated = true;
		return true;
	}

	void VideoPicture::Free()
	{
		if (allocated) {
			this->allocated = false;
			av_freep(&bmp.data[0]);
			av_freep(&bmp);
			height = width = 0;
		}
	}

///////////////////////////////////////////////////////////////////////////////////////////////////////////////

	SubtitlePictureQueue::SubtitlePictureQueue(_FFplayer *parent) : mutex(SDL_CreateMutex(), SDL_DestroyMutex),
		cond(SDL_CreateCond(), SDL_DestroyCond)
	{
		this->parent = parent;
	}

	SubtitlePictureQueue::~SubtitlePictureQueue()
	{
		clear();
	}

	void SubtitlePictureQueue::clear()
	{
		SDL_LockMutex(mutex.get());
		while (!queue.empty())
			queue.pop();
		SDL_UnlockMutex(mutex.get());
	}

	void SubtitlePictureQueue::put(std::shared_ptr<SubtitlePicture> Pic)
	{
		SDL_LockMutex(mutex.get());
		queue.push(Pic);
		SDL_CondSignal(cond.get());
		SDL_UnlockMutex(mutex.get());
	}

	int SubtitlePictureQueue::peek(std::shared_ptr<SubtitlePicture> &Pic)
	{
		int ret;
		SDL_LockMutex(mutex.get());
		while (true) {

			if (parent->IsQuit()) {
				ret = -1;
				break;
			}
			if (!queue.empty()) {
				Pic = queue.front();
				ret = 0;
				break;
			}
			else {
				ret = 1;
				break;
			}
			SDL_CondWait(cond.get(), mutex.get());
		}
		SDL_UnlockMutex(mutex.get());
		return ret;
	}

	int SubtitlePictureQueue::get(std::shared_ptr<SubtitlePicture> &Pic)
	{
		int ret;
		SDL_LockMutex(mutex.get());
		while (true) {

			if (parent->IsQuit()) {
				ret = -1;
				break;
			}
			if (!queue.empty()) {
				Pic = queue.front();
				queue.pop();
				ret = 0;
				break;
			}
			else {
				ret = 1;
				break;
			}
			SDL_CondWait(cond.get(), mutex.get());
		}
		SDL_UnlockMutex(mutex.get());
		return ret;
	}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	_Stream::_Stream(System::IO::Stream^ stream, System::Threading::CancellationToken^ ct) :
		buffer(reinterpret_cast<uint8_t *>(av_malloc(buffersize)), [](uint8_t *ptr) { }) //don't free. may be free internal
	{
		this->stream = stream;
		this->ct = ct;
	}

	_Stream::~_Stream() {
	}

	int _Stream::read_packet(void *opaque, uint8_t *buf, int buf_size) 
	{
		_Stream* me = (_Stream *)opaque;
		if (me->ct->IsCancellationRequested) return -1;
		array<Byte>^ byteArray = gcnew array<Byte>(buf_size);
		if (byteArray == nullptr) return 0;
		int ret = me->stream->Read(byteArray, 0, buf_size);
		if (ret > 0)
			Marshal::Copy(byteArray, 0, System::IntPtr(buf), ret);
		delete byteArray;
		return ret;
	}

	int64_t _Stream::seek(void *opaque, int64_t offset, int whence) 
	{
		_Stream* me = (_Stream *)opaque;
		if (me->ct->IsCancellationRequested) return -1;
		switch (whence)
		{
		case SEEK_SET:
			return me->stream->Seek(offset, SeekOrigin::Begin);
		case SEEK_CUR:
			return me->stream->Seek(offset, SeekOrigin::Current);
		case SEEK_END:
			return me->stream->Seek(offset, SeekOrigin::End);
		case AVSEEK_SIZE:
			return me->stream->Length;
		default:
			return -1;
		}
	}

///////////////////////////////////////////////////////////////////////////////////////////////////////////////

	_FFplayer::_FFplayer() :
		pictq_mutex(SDL_CreateMutex(), &SDL_DestroyMutex),
		pictq_cond(SDL_CreateCond(), &SDL_DestroyCond),
		screen_mutex(SDL_CreateMutex(), &SDL_DestroyMutex),
		audioq(this), videoq(this), subtitleq(this), subpictq(this),
		pictq_prev(&pictq[0]),
		ct(System::Threading::CancellationToken::None),
		funcs(this),
		audio_volume(50), av_sync_type(DEFAULT_AV_SYNC_TYPE)
	{

	}

	_FFplayer::~_FFplayer() 
	{
	}

	bool _FFplayer::Init()
	{
		// this function make "main thread",
		// so Play() should run with this thread.
		if (SDL_Init(SDL_INIT_VIDEO | SDL_INIT_AUDIO | SDL_INIT_TIMER | SDL_INIT_EVENTS)) {
			fprintf(stderr, "SDL: Init failed - exiting\n");
			return false;
		}
		if (TTF_Init() < 0) {
			fprintf(stderr, "TTF_Init() failed - %s\n", TTF_GetError());
			SDL_Quit();
			return false;
		}
		font = std::shared_ptr<TTF_Font>(TTF_OpenFont(fontfile.c_str(), fontsize), &TTF_CloseFont);
		if (font == NULL) {
			fprintf(stderr, "TTF_OpenFont failed - %s\n", TTF_GetError());
			TTF_Quit();
			SDL_Quit();
			return false;
		}

		screen = std::shared_ptr<SDLScreen>(new SDLScreen);

		avcodec_register_all();
		avfilter_register_all();
		av_register_all();

		if (!screen->window) {
			fprintf(stderr, "SDL: could not set video mode - exiting\n");
			font = NULL;
			TTF_Quit();
			SDL_Quit();
			return false;
		}
		if (IsFullscreen) ToggleFullscreen(true);

		return true;
	}

	void _FFplayer::Finalize()
	{
		Quit();
		inputstream->Quit();

		// avtivate threads for exit
		if (audioStream >= 0) {
			audioq.AbortQueue();
		}
		if (videoStream >= 0) {
			videoq.AbortQueue();
		}
		if (subtitleStream >= 0) {
			subtitleq.AbortQueue();
		}
		if (pictq)
			SDL_CondSignal(pictq_cond.get());
		// wait for exit threads
		if (parse_tid)
			SDL_WaitThread(parse_tid, NULL);
		parse_tid = 0;

		if (video_tid)
			SDL_WaitThread(video_tid, NULL);
		video_tid = 0;

		if (subtitle_tid)
			SDL_WaitThread(subtitle_tid, NULL);
		subtitle_tid = 0;

		if (audio_deviceID > 0)
			SDL_CloseAudioDevice(audio_deviceID);
		audio_deviceID = 0;

		destory_all_pictures();
		audioq.clear();
		videoq.clear();

		SDL_Event event;
		while (SDL_PollEvent(&event)) {
			// remove events
		}

		parse_tid = NULL;
		video_tid = NULL;
		sws_ctx = NULL;
		video_ctx = NULL;
		swr_ctx = NULL;
		audio_st = NULL;
		audio_ctx = NULL;
		inputstream = NULL;
		IOCtx = NULL;
		pFormatCtx = NULL;
		screen = NULL;
		agraph = NULL;

		filename[0] = '\0';
		videoStream = audioStream = -1;
		external_clock = 0;

		seek_req = 0;
		seek_flags = 0;
		seek_pos = 0;
		seek_rel = 0;

		audio_buf_size = 0;
		audio_buf_index = 0;
		audio_diff_cum = 0;
		audio_diff_avg_coef = 0;
		audio_diff_threshold = 0;
		audio_diff_avg_count = 0;
		audio_out_sample_rate = 0;
		audio_out_channels = 0;
		audio_clock = 0;
		audio_filter_src = { 0 };

		frame_timer = 0;
		frame_last_pts = 0;
		frame_last_delay.clear();

		video_st = 0;
		video_clock = 0;
		video_current_pts = 0;
		video_current_pts_time = 0;

		pictq_size, pictq_rindex, pictq_windex = 0;

		font = NULL;

		audio_only = false;
		audio_eof = audio_eof_enum::playing;
		video_eof = false;
		quit = false;
	}

	bool _FFplayer::IsQuit()
	{
		if (ct->IsCancellationRequested) Quit();
		return quit || ct->IsCancellationRequested || (audio_eof == audio_eof_enum::eof && video_eof);
	}

	int _FFplayer::decode_interrupt_cb(void *ctx)
	{
		_FFplayer *is = (_FFplayer *)ctx;
		return is->IsQuit() || is->seek_flags;
	}

	Uint32 _FFplayer::sdl_refresh_timer_cb(Uint32 interval, void *opaque)
	{
		SDL_Event event;
		memset(&event, 0, sizeof(event));
		event.type = FF_REFRESH_EVENT;
		event.user.data1 = opaque;
		SDL_PushEvent(&event);
		return 0; /* 0 means stop timer */
	}

	Uint32 _FFplayer::sdl_internal_refresh_timer_cb(Uint32 interval, void *opaque)
	{
		SDL_Event event;
		memset(&event, 0, sizeof(event));
		event.type = FF_INTERNAL_REFRESH_EVENT;
		event.user.data1 = opaque;
		SDL_PushEvent(&event);
		return 0; /* 0 means stop timer */
	}

	/* schedule a video refresh in 'delay' ms */
	void _FFplayer::schedule_refresh(int delay)
	{
		InterlockedIncrement(&remove_refresh);
		SDL_AddTimer(delay, sdl_refresh_timer_cb, this);
	}


	double _FFplayer::get_audio_clock() 
	{
		double pts;
		int hw_buf_size, bytes_per_sec, n;

		pts = audio_clock; /* maintained in the audio thread */
		hw_buf_size = audio_buf_size - audio_buf_index;
		bytes_per_sec = 0;
		n = audio_out_channels * 2;
		if (audio_st) {
			bytes_per_sec = audio_out_sample_rate * n;
		}
		if (bytes_per_sec) {
			pts -= (double)hw_buf_size / bytes_per_sec;
		}
		return pts;
	}


	double _FFplayer::get_video_clock()
	{
		double delta;

		delta = (av_gettime() - video_current_pts_time) / 1000000.0;
		return video_current_pts + delta;
	}


	double _FFplayer::get_external_clock()
	{
		return av_gettime() / 1000000.0;
	}


	double _FFplayer::get_master_clock()
	{
		if (av_sync_type == AV_SYNC_VIDEO_MASTER) {
			return get_video_clock();
		}
		else if (av_sync_type == AV_SYNC_AUDIO_MASTER) {
			return get_audio_clock();
		}
		else {
			return get_external_clock();
		}
	}

	double _FFplayer::get_master_clock_start()
	{
		if (av_sync_type == AV_SYNC_VIDEO_MASTER) {
			return video_clock_start;
		}
		else if (av_sync_type == AV_SYNC_AUDIO_MASTER) {
			return audio_clock_start;
		}
		else {
			return external_clock;
		}
	}

	bool _FFplayer::configure_audio_filters()
	{
		char asrc_args[256] = { 0 };
		std::shared_ptr<AVFilterGraph> graph(avfilter_graph_alloc(), [](AVFilterGraph *ptr) { avfilter_graph_free(&ptr); });
		AVFilterContext *filt_asrc = NULL, *filt_asink = NULL;
		AVFilterContext *filt_volume = NULL;
		AVFilterContext *filt_loudnorm = NULL;
		AVFilterContext *filt_aresample = NULL;

		afilt_in = NULL;
		afilt_out = NULL;
		agraph = NULL;

		if (!graph) return false;

		int ret = snprintf(asrc_args, sizeof(asrc_args),
			"sample_rate=%d:sample_fmt=%s:channels=%d:time_base=%d/%d",
			audio_filter_src.freq, av_get_sample_fmt_name(audio_filter_src.fmt),
			audio_filter_src.channels,
			1, audio_filter_src.freq);
		if (audio_filter_src.channel_layout)
			snprintf(asrc_args + ret, sizeof(asrc_args) - ret,
				":channel_layout=0x%" PRIx64, audio_filter_src.channel_layout);

		if (avfilter_graph_create_filter(&filt_asrc,
			avfilter_get_by_name("abuffer"), "ffplay_abuffer",
			asrc_args, NULL, graph.get()) < 0)
			return false;

		if (avfilter_graph_create_filter(&filt_asink,
			avfilter_get_by_name("abuffersink"), "ffplay_abuffersink",
			NULL, NULL, graph.get()) < 0)
			return false;

		if (audio_filter_src.audio_volume_dB != 0) {
			snprintf(asrc_args, sizeof(asrc_args),
				"volume=%ddB", audio_filter_src.audio_volume_dB);
			if (avfilter_graph_create_filter(&filt_volume,
				avfilter_get_by_name("volume"), "ffplay_volume",
				asrc_args, NULL, graph.get()) < 0)
				return false;
		}
		if (audio_filter_src.audio_volume_auto) {
			snprintf(asrc_args, sizeof(asrc_args),
				"f=500");
			if (avfilter_graph_create_filter(&filt_loudnorm,
				avfilter_get_by_name("dynaudnorm"), "ffplay_dynaudnorm",
				asrc_args, NULL, graph.get()) < 0)
				return false;
		}
		if (audio_filter_src.freq != audio_out_sample_rate) {
			snprintf(asrc_args, sizeof(asrc_args),
				"%d", audio_out_sample_rate);
			if (avfilter_graph_create_filter(&filt_aresample,
				avfilter_get_by_name("aresample"), "ffplay_resample",
				asrc_args, NULL, graph.get()) < 0)
				return false;
		}

		AVFilterContext *filt_last = filt_asrc;
		if (filt_loudnorm) {
			if (avfilter_link(filt_last, 0, filt_loudnorm, 0) != 0)
				return false;
			filt_last = filt_loudnorm;
		}
		if (filt_volume) {
			if (avfilter_link(filt_last, 0, filt_volume, 0) != 0)
				return false;
			filt_last = filt_volume;
		}
		if (filt_aresample) {
			if (avfilter_link(filt_last, 0, filt_aresample, 0) != 0)
				return false;
			filt_last = filt_aresample;
		}
		if (avfilter_link(filt_last, 0, filt_asink, 0) != 0)
			return false;

		if (avfilter_graph_config(graph.get(), NULL) < 0)
			return false;

		afilt_in = filt_asrc;
		afilt_out = filt_asink;
		agraph = graph;
		return true;
	}

	static inline
	int64_t get_valid_channel_layout(int64_t channel_layout, int channels)
	{
		if (channel_layout && av_get_channel_layout_nb_channels(channel_layout) == channels)
			return channel_layout;
		else
			return 0;
	}

	static inline
	int cmp_audio_fmts(enum AVSampleFormat fmt1, int64_t channel_count1,
			enum AVSampleFormat fmt2, int64_t channel_count2)
	{
		/* If channel count == 1, planar and non-planar formats are the same */
		if (channel_count1 == 1 && channel_count2 == 1)
			return av_get_packed_sample_fmt(fmt1) != av_get_packed_sample_fmt(fmt2);
		else
			return channel_count1 != channel_count2 || fmt1 != fmt2;
	}

	int _FFplayer::audio_decode_frame(uint8_t *audio_buf, int buf_size, double *pts_ptr)
	{
		int buf_limit = buf_size * 3 / 4;
		AVCodecContext *aCodecCtx = audio_ctx.get();
		SwrContext *a_convert_ctx = swr_ctx.get();
		AVPacket pkt = { 0 }, *inpkt = &pkt;
		AVFrame audio_frame_in = { 0 }, *inframe = &audio_frame_in;
		AVFrame audio_frame_out = { 0 };

		while(true) {
			int ret;
			if (seek_req) {
				audio_clock = NAN;
				av_log(NULL, AV_LOG_INFO, "seeking audio mute\n");
				goto quit_audio;
			}
			if (inpkt) {
				if ((ret = audioq.get(inpkt, 0)) < 0) {
					av_log(NULL, AV_LOG_INFO, "audio Quit\n");
					audio_eof = audio_eof_enum::eof;
					goto quit_audio;
				}
				if (audio_eof == audio_eof_enum::playing && ret == 0) {
					av_log(NULL, AV_LOG_INFO, "audio queue empty\n");
					goto quit_audio;
				}
				if (inpkt->data == flush_pkt.data) {
					av_log(NULL, AV_LOG_INFO, "audio buffer flush\n");
					avcodec_flush_buffers(aCodecCtx);
					pkt = { 0 };
					audio_serial = av_gettime();
					inpkt = &pkt;
					inframe = &audio_frame_in;
					continue;
				}
				if (inpkt->data == eof_pkt.data) {
					av_log(NULL, AV_LOG_INFO, "audio buffer EOF\n");
					audio_eof = audio_eof_enum::input_eof;
				}
				if (inpkt->data == abort_pkt.data) {
					av_log(NULL, AV_LOG_INFO, "audio buffer ABORT\n");
					audio_eof = audio_eof_enum::eof;
					goto quit_audio;
				}
			}
			if (audio_eof >= audio_eof_enum::input_eof) {
				inpkt = NULL;
				if (audio_eof == audio_eof_enum::output_eof) {
					audio_eof = audio_eof_enum::eof;
					av_log(NULL, AV_LOG_INFO, "audio EOF\n");
					goto quit_audio;
				}
			}

			// send packet to codec context
			ret = avcodec_send_packet(aCodecCtx, inpkt);
			if (ret >= 0 || (audio_eof == audio_eof_enum::input_eof && ret == AVERROR_EOF)) {
				if (inpkt) av_packet_unref(inpkt);
				int data_size = 0;

				// Decode audio frame
				while (ret = avcodec_receive_frame(aCodecCtx, inframe), ret >= 0 || ret == AVERROR_EOF) {
					if (ret == AVERROR_EOF)
						inframe = NULL;

					if (inframe) {
						auto dec_channel_layout = get_valid_channel_layout(inframe->channel_layout, av_frame_get_channels(inframe));
						if (!dec_channel_layout)
							dec_channel_layout = av_get_default_channel_layout(inframe->channels);
						bool reconfigure =
							cmp_audio_fmts(audio_filter_src.fmt, audio_filter_src.channels,
							(enum AVSampleFormat)inframe->format, av_frame_get_channels(inframe)) ||
							audio_filter_src.channel_layout != dec_channel_layout ||
							audio_filter_src.freq != inframe->sample_rate ||
							audio_filter_src.audio_volume_dB != audio_volume_dB ||
							audio_filter_src.audio_volume_auto != audio_volume_auto ||
							audio_filter_src.serial != audio_serial;

						if (reconfigure) {
							audio_filter_src.fmt = (enum AVSampleFormat)inframe->format;
							audio_filter_src.channels = av_frame_get_channels(inframe);
							audio_filter_src.channel_layout = dec_channel_layout;
							audio_filter_src.freq = inframe->sample_rate;
							audio_filter_src.audio_volume_dB = audio_volume_dB;
							audio_filter_src.audio_volume_auto = audio_volume_auto;
							audio_filter_src.serial = audio_serial;

							if (!configure_audio_filters())
								goto quit_audio;

							a_convert_ctx = swr_alloc_set_opts(NULL,
								av_get_default_channel_layout(audio_out_channels), AV_SAMPLE_FMT_S16, audio_out_sample_rate,
								afilt_out->inputs[0]->channel_layout, (enum AVSampleFormat)afilt_out->inputs[0]->format, afilt_out->inputs[0]->sample_rate,
								0, NULL);
							swr_init(a_convert_ctx);

							swr_ctx = std::shared_ptr<SwrContext>(a_convert_ctx, [](SwrContext *ptr) { swr_free(&ptr); });
						}
					}

					if (!afilt_in || !afilt_out)
						goto quit_audio;

					if (av_buffersrc_add_frame(afilt_in, inframe) < 0)
						goto quit_audio;

					if (inframe) av_frame_unref(inframe);
					while (buf_size > buf_limit && (ret = av_buffersink_get_frame(afilt_out, &audio_frame_out)) >= 0) {

						int64_t pts_t;
						if ((pts_t = av_frame_get_best_effort_timestamp(&audio_frame_out)) != AV_NOPTS_VALUE) {
							audio_clock = av_q2d(audio_st->time_base)*pts_t;
							//av_log(NULL, AV_LOG_INFO, "audio clock %f\n", audio_clock);
							if (isnan(audio_clock_start)) {
								audio_clock_start = audio_clock;
							}
						}

						int out_samples = (int)av_rescale_rnd(swr_get_delay(a_convert_ctx, audio_frame_out.sample_rate) +
							audio_frame_out.nb_samples, audio_out_sample_rate, audio_frame_out.sample_rate, AV_ROUND_UP);
						int out_size = av_samples_get_buffer_size(NULL,
							audio_out_channels,
							out_samples,
							AV_SAMPLE_FMT_S16,
							1);
						assert(out_size <= buf_size);
						swr_convert(a_convert_ctx, &audio_buf, out_samples, (const uint8_t **)audio_frame_out.data, audio_frame_out.nb_samples);
						audio_buf += out_size;
						buf_size -= out_size;
						data_size += out_size;

						int n = 2 * audio_out_channels;
						audio_clock += (double)out_size /
							(double)(n * audio_out_sample_rate);

						av_frame_unref(&audio_frame_out);
					}//while(av_buffersink_get_frame)

					if (ret == AVERROR_EOF) {
						audio_eof = audio_eof_enum::output_eof;
						av_log(NULL, AV_LOG_INFO, "audio output EOF\n");
					}

					if (!inframe) break;
				}//while(avcodec_receive_frame)
				
				if (data_size > 0) {
					double pts = audio_clock;
					*pts_ptr = pts;
					/* We have data, return it and come back for more later */
					return data_size;
				}
			} //if (avcodec_send_packet)
			if(inpkt) av_packet_unref(inpkt);

			if (IsQuit()) {
				goto quit_audio;
			}
		}//while(true)
	quit_audio:
		av_log(NULL, AV_LOG_INFO, "audio Pause\n");
		SDL_PauseAudioDevice(audio_deviceID, 1);
		audio_pause = true;
		return -1;
	}

	/* Add or subtract samples to get a better sync, return new
	audio buffer size */
	int _FFplayer::synchronize_audio(short *samples,
		int samples_size, double pts)
	{
		int n = 2 * audio_ctx->channels;

		if (av_sync_type != AV_SYNC_AUDIO_MASTER) {

			double ref_clock = get_master_clock();
			double diff = get_audio_clock() - ref_clock;

			if (diff < AV_NOSYNC_THRESHOLD) {
				// accumulate the diffs
				audio_diff_cum = diff + audio_diff_avg_coef
					* audio_diff_cum;
				if (audio_diff_avg_count < AUDIO_DIFF_AVG_NB) {
					audio_diff_avg_count++;
				}
				else {
					double avg_diff = audio_diff_cum * (1.0 - audio_diff_avg_coef);
					if (fabs(avg_diff) >= audio_diff_threshold) {
						int wanted_size = samples_size + ((int)(diff * audio_ctx->sample_rate) * n);
						int min_size = samples_size * ((100 - SAMPLE_CORRECTION_PERCENT_MAX) / 100);
						int max_size = samples_size * ((100 + SAMPLE_CORRECTION_PERCENT_MAX) / 100);
						if (wanted_size < min_size) {
							wanted_size = min_size;
						}
						else if (wanted_size > max_size) {
							wanted_size = max_size;
						}
						if (wanted_size < samples_size) {
							/* remove samples */
							samples_size = wanted_size;
						}
						else if (wanted_size > samples_size) {
							uint8_t *samples_end, *q;
							int nb;

							/* add samples by copying final sample*/
							nb = (samples_size - wanted_size);
							samples_end = (uint8_t *)samples + samples_size - n;
							q = samples_end + n;
							while (nb > 0) {
								memcpy(q, samples_end, n);
								q += n;
								nb -= n;
							}
							samples_size = wanted_size;
						}
					}
				}
			}
			else {
				/* difference is TOO big; reset diff stuff */
				audio_diff_avg_count = 0;
				audio_diff_cum = 0;
			}
		}
		return samples_size;
	}


	void _FFplayer::audio_callback(void *userdata, Uint8 *stream, int len)
	{
		_FFplayer *is = (_FFplayer *)userdata;
		int len1, audio_size;
		double pts;

		while (len > 0) {
			if (is->audio_buf_index >= is->audio_buf_size) {
				/* We have already sent all our data; get more */
				audio_size = is->audio_decode_frame(is->audio_buf, sizeof(is->audio_buf), &pts);
				if (audio_size < 0) {
					/* If error, output silence */
					is->audio_buf_size = MAX_AUDIO_FRAME_SIZE;
					memset(is->audio_buf, 0, is->audio_buf_size);
				}
				else {
					audio_size = is->synchronize_audio((int16_t *)is->audio_buf,
						audio_size, pts);
					is->audio_buf_size = audio_size;
				}
				is->audio_buf_index = 0;
			}
			len1 = is->audio_buf_size - is->audio_buf_index;
			if (len1 > len)
				len1 = len;
			memset(stream, 0, len1);
			int volume = (is->audio_mute)? 0: (int)(is->audio_volume / 100 * SDL_MIX_MAXVOLUME);
			SDL_MixAudioFormat(stream, is->audio_buf + is->audio_buf_index, AUDIO_S16SYS, len1, volume);
			len -= len1;
			stream += len1;
			is->audio_buf_index += len1;
		}

		is->audio_callback_time = (double)av_gettime() / 1000000.0;
	}

	void _FFplayer::destory_pictures()
	{
		SDL_LockMutex(pictq_mutex.get());
		for (VideoPicture *p = pictq; p < &pictq[VIDEO_PICTURE_QUEUE_SIZE]; p++) {
			if (p == &pictq[pictq_windex]) continue;
			p->Free();
			p->pts = 0;
		}
		pictq_size = 1;
		SDL_CondSignal(pictq_cond.get());
		SDL_UnlockMutex(pictq_mutex.get());
	}

	void _FFplayer::destory_all_pictures()
	{
		SDL_LockMutex(pictq_mutex.get());
		for (VideoPicture *p = pictq; p < &pictq[VIDEO_PICTURE_QUEUE_SIZE]; p++) {
			p->Free();
			p->pts = 0;
		}
		pictq_size = 0;
		SDL_CondSignal(pictq_cond.get());
		SDL_UnlockMutex(pictq_mutex.get());
	}

	int _FFplayer::queue_picture(AVFrame *pFrame, double pts)
	{
		VideoPicture *vp;

		/* wait until we have space for a new pic */
		SDL_LockMutex(pictq_mutex.get());
		while (!IsQuit() && pictq_size >= VIDEO_PICTURE_QUEUE_SIZE-1) {
			SDL_CondWait(pictq_cond.get(), pictq_mutex.get());
		}
		SDL_UnlockMutex(pictq_mutex.get());

		if (IsQuit())
			return -1;

		// windex is set to 0 initially
		vp = &pictq[pictq_windex];

		int width = video_width;
		int height = video_height;

		/* allocate or resize the buffer! */
		if (!vp->allocated ||
			vp->width != width ||
			vp->height != height) {

			vp->Allocate(width, height);
			if (IsQuit()) {
				return -1;
			}
		}

		/* We have a place to put our picture on the queue */
		if (vp->bmp.data) {
			// Convert the image into YUV format that SDL uses
			sws_scale(sws_ctx.get(), (uint8_t const * const *)pFrame->data,
				pFrame->linesize, 0, pFrame->height,
				vp->bmp.data, vp->bmp.linesize);

			vp->pts = pts;
			vp->serial = av_gettime();

			/* now we inform our display thread that we have a pic ready */
			if (++pictq_windex == VIDEO_PICTURE_QUEUE_SIZE) {
				pictq_windex = 0;
			}
			SDL_LockMutex(pictq_mutex.get());
			pictq_size++;
			SDL_UnlockMutex(pictq_mutex.get());
		}
		return 0;
	}

	double _FFplayer::synchronize_video(AVFrame *src_frame, double pts, double framerate)
	{
		double frame_delay;

		if (pts != 0) {
			/* if we have pts, set video clock to it */
			video_clock = pts;
		}
		else {
			/* if we aren't given a pts, set it to the clock */
			pts = video_clock;
		}
		/* update the video clock */
		frame_delay = 1 / framerate;
		/* if we are repeating a frame, adjust clock accordingly */
		frame_delay += src_frame->repeat_pict * (frame_delay * 0.5);
		video_clock += frame_delay;
		return video_clock;
	}

	bool _FFplayer::Configure_VideoFilter(AVFilterContext **filt_in, AVFilterContext **filt_out, AVFrame *frame, AVFilterGraph *graph)
	{
		AVFilterContext *filt_deint = NULL;
		char args[256];

		snprintf(args, sizeof(args),
			"video_size=%dx%d:pix_fmt=%d:time_base=%d/%d:pixel_aspect=%d/%d",
			frame->width, frame->height, frame->format,
			video_st->time_base.num, video_st->time_base.den,
			video_st->codecpar->sample_aspect_ratio.num, FFMAX(video_st->codecpar->sample_aspect_ratio.den, 1));
		AVRational fr = av_guess_frame_rate(pFormatCtx.get(), video_st, NULL);
		if (fr.num && fr.den)
			av_strlcatf(args, sizeof(args), ":frame_rate=%d/%d", fr.num, fr.den);

		if (avfilter_graph_create_filter(
			filt_in,
			avfilter_get_by_name("buffer"),
			"buffer",
			args,
			NULL,
			graph
		) < 0)
			return false;
		if (avfilter_graph_create_filter(
			filt_out,
			avfilter_get_by_name("buffersink"),
			"buffersink",
			NULL,
			NULL,
			graph
		) < 0)
			return false;
		if (deinterlace) {
			snprintf(args, sizeof(args), "mode=1:parity=-1:deint=1");
			if (avfilter_graph_create_filter(
				&filt_deint,
				avfilter_get_by_name("bwdif"),
				"deinterlace",
				args,
				NULL,
				graph
			) < 0)
				return false;
			if (avfilter_link(filt_deint, 0, *filt_out, 0) != 0)
				return false;
			if (avfilter_link(*filt_in, 0, filt_deint, 0) != 0)
				return false;
		}
		else {
			if (avfilter_link(*filt_in, 0, *filt_out, 0) != 0)
				return false;
		}
		return (avfilter_graph_config(graph, NULL) >= 0);
	}

	int _FFplayer::video_thread(void *arg)
	{
		av_log(NULL, AV_LOG_INFO, "video_thread start\n");
		_FFplayer *is = (_FFplayer *)arg;
		AVPacket packet = { 0 }, *inpkt = &packet;
		AVCodecContext *video_ctx = is->video_ctx.get();
		AVFrame frame = { 0 }, *inframe = &frame;
		std::shared_ptr<AVFilterGraph> graph(avfilter_graph_alloc(), [](AVFilterGraph *ptr) { avfilter_graph_free(&ptr); });
		AVFilterContext *filt_out = NULL, *filt_in = NULL;
		int last_w = 0;
		int last_h = 0;
		AVPixelFormat last_format = (AVPixelFormat)-2;
		int64_t last_serial = 0, serial = 0;
		is->pictq_active_serial = 0;
		AVRational frame_rate = av_guess_frame_rate(is->pFormatCtx.get(), is->video_st, NULL);

		switch (is->video_ctx->codec_id)
		{
		case AV_CODEC_ID_MJPEG:
		case AV_CODEC_ID_MJPEGB:
		case AV_CODEC_ID_LJPEG:
			is->deinterlace = false;
			break;
		default:
			is->deinterlace = true;
			break;
		}

		av_log(NULL, AV_LOG_INFO, "video_thread read loop\n");
		std::deque<double> lastpts;
		double pts = 0;
		double prevpts = NAN;
		while (true) {
			video_ctx = is->video_ctx.get();
			if (is->video_eof) {
				while (!is->IsQuit() && is->videoq.get(&packet, 0) == 0)
					SDL_Delay(100);
			}
			else if (is->videoq.get(&packet, 1) < 0) {
				// means we quit getting packets
				av_log(NULL, AV_LOG_INFO, "video Quit\n");
				is->video_eof = true;
				packet = { 0 };
				break;
			}
			if (is->IsQuit()) break;

			if (packet.data == flush_pkt.data) {
				av_log(NULL, AV_LOG_INFO, "video buffer flush\n");
				avcodec_flush_buffers(video_ctx);
				packet = { 0 };
				inpkt = &packet;
				inframe = &frame;
				is->video_eof = false;
				serial = av_gettime();
				is->pictq_active_serial = serial;
				continue;
			}
			if (packet.data == eof_pkt.data) {
				av_log(NULL, AV_LOG_INFO, "video buffer EOF\n");
				packet = { 0 };
				inpkt = NULL;
			}
			if (packet.data == abort_pkt.data) {
				av_log(NULL, AV_LOG_INFO, "video buffer ABORT\n");
				is->video_eof = true;
				packet = { 0 };
				break;
			}
			// send packet to codec context
			if (avcodec_send_packet(video_ctx, inpkt) >= 0) {

				// Decode video frame
				int ret;
				while (ret = avcodec_receive_frame(video_ctx, &frame), ret >= 0 || ret == AVERROR_EOF) {
					if (ret == AVERROR_EOF)
						inframe = NULL;

					if (inframe) {
						if (frame.width != last_w || 
							frame.height != last_h ||
							frame.format != last_format ||
							last_serial != serial) {
							graph = std::shared_ptr<AVFilterGraph>(avfilter_graph_alloc(), [](AVFilterGraph *ptr) { avfilter_graph_free(&ptr); });
							if (!is->Configure_VideoFilter(&filt_in, &filt_out, &frame, graph.get())) {
								is->Quit();
								return 1;
							}
							last_w = frame.width;
							last_h = frame.height;
							last_format = (AVPixelFormat)frame.format;
							last_serial = serial;
						}
						int repeat = inframe->repeat_pict;
					}
					

					if (av_buffersrc_write_frame(filt_in, inframe) < 0)
						return 1;

					if (inframe) av_frame_unref(inframe);
					while (av_buffersink_get_frame(filt_out, &frame) >= 0) {

						if (frame.width != is->video_srcwidth || frame.height != is->video_srcheight
							|| frame.sample_aspect_ratio.den != is->video_SAR.den || frame.sample_aspect_ratio.num != is->video_SAR.num)
						{
							is->sws_ctx = NULL;
							is->video_SAR = frame.sample_aspect_ratio;
							double aspect_ratio = 0;
							if (video_ctx->sample_aspect_ratio.num == 0) {
								aspect_ratio = 0;
							}
							else {
								aspect_ratio = av_q2d(video_ctx->sample_aspect_ratio) *
									frame.width / frame.height;
							}
							if (aspect_ratio <= 0.0) {
								aspect_ratio = (double)frame.width /
									(double)frame.height;
							}
							is->video_height = video_ctx->height;
							is->video_width = ((int)rint(is->video_height * aspect_ratio)) & ~1;

							uint64_t t = av_gettime();
							while (!is->screen->RequestSrcSize(is->video_width, is->video_height, t))
								SDL_Delay(100);
							SDL_Event evnt = { 0 };
							evnt.type = SDL_WINDOWEVENT;
							evnt.window.event = SDL_WINDOWEVENT_RESIZED;
							evnt.window.windowID = is->screen->GetWindowID();
							if (is->screenauto) {
								evnt.window.data1 = is->video_width;
								evnt.window.data2 = is->video_height;
							}
							else {
								evnt.window.data1 = is->screenwidth;
								evnt.window.data2 = is->screenheight;
							}
							SDL_PushEvent(&evnt);
							while (!is->screen->IsFinished(t))
								SDL_Delay(100);
							if (!is->screenauto) {
								is->screen->SetPosition(is->screenxpos, is->screenypos);
							}

							// initialize SWS context for software scaling
							is->video_srcheight = frame.height;
							is->video_srcwidth = frame.width;
							is->sws_ctx = std::shared_ptr<SwsContext>(
								sws_getCachedContext(NULL,
									is->video_srcwidth, is->video_srcheight,
									video_ctx->pix_fmt, is->video_width,
									is->video_height, AV_PIX_FMT_YUV420P,
									SWS_BICUBLIN, NULL, NULL, NULL
								), &sws_freeContext);
						} //if src.size != frame.size

						int64_t pts_t;
						if ((pts_t = av_frame_get_best_effort_timestamp(&frame)) != AV_NOPTS_VALUE) {
							pts = pts_t * av_q2d(is->video_st->time_base);
							//av_log(NULL, AV_LOG_INFO, "video clock %f\n", pts);

							if (isnan(is->video_clock_start)) {
								is->video_clock_start = pts;
							}
						}

						if (pts > 0) {
							lastpts.push_back(pts);
						}

						if (fabs(prevpts - pts) < 1.0e-6 || pts == 0) {
							if (lastpts.size() > 1) {
								double p = lastpts.front();
								double dpts = 0;
								for each(auto i in lastpts) {
									dpts += i - p;
									p = i;
								}
								dpts /= (lastpts.size() - 1);

								pts += dpts;
							}
						}
						if (pts > 0)
							prevpts = pts;
						if (lastpts.size() > 30)
							lastpts.pop_front();

						frame_rate = filt_out->inputs[0]->frame_rate;
						pts = is->synchronize_video(&frame, pts, av_q2d(frame_rate));
						if (is->queue_picture(&frame, pts) < 0) {
							return 1;
						}
						av_frame_unref(&frame);
					} //while(av_buffersink_get_frame)

					if (!inframe) {
						break;
					}
				} //while(avcodec_receive_frame)

				av_frame_unref(&frame);
			} //if(avcodec_send_packet)

			if (inpkt) av_packet_unref(inpkt);
			if (!inframe) {
				is->video_eof = true;
			}
		}//while(true)

		if (is->audio_eof == audio_eof_enum::eof) is->Quit();
		return 0;
	}

	int _FFplayer::subtitle_thread(void *arg)
	{
		_FFplayer *is = (_FFplayer *)arg;
		AVPacket packet = { 0 };
		std::shared_ptr<SwsContext> sub_convert_ctx;
		int64_t old_serial = 0;

		is->subpictq_active_serial = av_gettime();
		av_log(NULL, AV_LOG_INFO, "subtitle thread start\n");
		while (!is->IsQuit()) {
			AVCodecContext *subtitle_ctx = is->subtitle_ctx.get();
			if (is->subtitleq.get(&packet, 1) < 0) {
				// means we quit getting packets
				av_log(NULL, AV_LOG_INFO, "subtitle Quit\n");
				break;
			}
		retry:
			if (packet.data == flush_pkt.data) {
				av_log(NULL, AV_LOG_INFO, "subtitle buffer flush\n");
				avcodec_flush_buffers(subtitle_ctx);
				packet = { 0 };
				is->subpictq_active_serial = av_gettime();
				continue;
			}
			if (packet.data == eof_pkt.data) {
				av_log(NULL, AV_LOG_INFO, "subtitle buffer EOF\n");
				packet = { 0 };
				while (!is->IsQuit() && is->subtitleq.get(&packet, 0) == 0)
					SDL_Delay(100);
				if (is->IsQuit()) break;
				goto retry;
			}
			if (packet.data == abort_pkt.data) {
				av_log(NULL, AV_LOG_INFO, "subtitle buffer ABORT\n");
				packet = { 0 };
				break;
			}
			int got_frame = 0;
			int ret;
			AVSubtitle sub;
			if ((ret = avcodec_decode_subtitle2(subtitle_ctx, &sub, &got_frame, &packet)) < 0) {
				av_packet_unref(&packet);
				char buf[AV_ERROR_MAX_STRING_SIZE];
				char *errstr = av_make_error_string(buf, AV_ERROR_MAX_STRING_SIZE, ret);
				av_log(NULL, AV_LOG_ERROR, "error avcodec_decode_subtitle2() %d %s\n", ret, errstr);
				return -1;
			}
			av_packet_unref(&packet);
			if (got_frame == 0) continue;

			double pts = 0;

			if (sub.pts != AV_NOPTS_VALUE)
				pts = sub.pts / (double)AV_TIME_BASE;
			std::shared_ptr<SubtitlePicture> sp(new SubtitlePicture);
			sp->pts = pts;
			sp->serial = av_gettime();
			while (old_serial >= sp->serial) sp->serial++;
			old_serial = sp->serial;
			sp->start_display_time = sub.start_display_time;
			sp->end_display_time = sub.end_display_time;
			sp->numrects = sub.num_rects;
			sp->subrects.reset(new std::shared_ptr<AVSubtitleRect>[sub.num_rects]());
			sp->type = sub.format;
			for (size_t i = 0; i < sub.num_rects; i++)
			{
				sp->subw = subtitle_ctx->width ? subtitle_ctx->width : is->video_ctx->width;
				sp->subh = subtitle_ctx->height ? subtitle_ctx->height : is->video_ctx->height;

				if (((sp->subrects[i] = std::shared_ptr<AVSubtitleRect>(
					(AVSubtitleRect *)av_mallocz(sizeof(AVSubtitleRect)),
					[](AVSubtitleRect *p) {
					if (p->text)
						av_free(p->text);
					if (p->ass)
						av_free(p->ass);
					if (p->data[0])
						av_free(p->data[0]);
					av_free(p);
				})) == NULL)) {
					av_log(NULL, AV_LOG_FATAL, "Cannot allocate subtitle data\n");
					return -1;
				}

				sp->subrects[i]->type = sub.rects[i]->type;
				if (sub.rects[i]->ass)
					sp->subrects[i]->ass = av_strdup(sub.rects[i]->ass);
				if (sub.rects[i]->text)
					sp->subrects[i]->text = av_strdup(sub.rects[i]->text);
				if (sub.format == 0) {
					if (av_image_alloc(sp->subrects[i]->data, sp->subrects[i]->linesize, sub.rects[i]->w, sub.rects[i]->h, AV_PIX_FMT_ARGB, 16) < 0) {
						av_log(NULL, AV_LOG_FATAL, "Cannot allocate subtitle data\n");
						return -1;
					}
					sub_convert_ctx = std::shared_ptr<SwsContext>(sws_getCachedContext(NULL,
						sub.rects[i]->w, sub.rects[i]->h, AV_PIX_FMT_PAL8,
						sub.rects[i]->w, sub.rects[i]->h, AV_PIX_FMT_ARGB,
						SWS_BICUBIC, NULL, NULL, NULL), &sws_freeContext);
					if (!sub_convert_ctx) {
						av_log(NULL, AV_LOG_FATAL, "Cannot initialize the sub conversion context\n");
						return -1;
					}
					sws_scale(sub_convert_ctx.get(),
						sub.rects[i]->data, sub.rects[i]->linesize,
						0, sub.rects[i]->h, sp->subrects[i]->data, sp->subrects[i]->linesize);
					sp->subrects[i]->w = sub.rects[i]->w;
					sp->subrects[i]->h = sub.rects[i]->h;
					sp->subrects[i]->x = sub.rects[i]->x;
					sp->subrects[i]->y = sub.rects[i]->y;
				}
			}
			is->subpictq.put(sp);
			avsubtitle_free(&sub);
		}
		return 0;
	}

	bool _FFplayer::SetScreenSize(int width, int height, int src_width, int src_height)
	{
		if (SDL_GetThreadID(NULL) != mainthread) {
			uint64_t t = av_gettime();
			while (!screen->RequestSrcSize(src_width, src_height, t) && !IsQuit())
				SDL_Delay(100);
			if (IsQuit()) return false;

			SDL_Event evnt = { 0 };
			evnt.type = SDL_WINDOWEVENT;
			evnt.window.event = SDL_WINDOWEVENT_RESIZED;
			evnt.window.windowID = screen->GetWindowID();
			evnt.window.data1 = width;
			evnt.window.data2 = height;
			SDL_PushEvent(&evnt);

			while (!screen->IsFinished(t) && !IsQuit())
				SDL_Delay(100);
			if (IsQuit()) return false;
		}
		else {
			SDL_LockMutex(screen_mutex.get());
			screen->SetScreenSize(width, height, src_width, src_height);
			SDL_UnlockMutex(screen_mutex.get());
		}
		return true;
	}

	int _FFplayer::stream_component_open(int stream_index)
	{
		std::shared_ptr<AVCodecContext> codecCtx;
		AVCodec *codec;

		if (stream_index < 0 || (unsigned)stream_index >= pFormatCtx->nb_streams) {
			return -1;
		}

		codec = avcodec_find_decoder(pFormatCtx->streams[stream_index]->codecpar->codec_id);
		if (!codec) {
			av_log(NULL, AV_LOG_PANIC, "Unsupported codec!\n");
			return -1;
		}

		codecCtx = std::shared_ptr<AVCodecContext>(avcodec_alloc_context3(codec), [](AVCodecContext *ptr) {avcodec_free_context(&ptr); });
		if (avcodec_parameters_to_context(codecCtx.get(), pFormatCtx->streams[stream_index]->codecpar) < 0) {
			av_log(NULL, AV_LOG_PANIC, "Couldn't copy codec parameter to codec context\n");
			return -1;
		}
		av_codec_set_pkt_timebase(codecCtx.get(), pFormatCtx->streams[stream_index]->time_base);

		SDL_AudioSpec wanted_spec, spec;

		if (codecCtx->codec_type == AVMEDIA_TYPE_AUDIO) {
			wanted_spec.freq = codecCtx->sample_rate;
			wanted_spec.format = AUDIO_S16SYS;
			wanted_spec.channels = codecCtx->channels;
			wanted_spec.silence = 0;
			wanted_spec.samples = SDL_AUDIO_BUFFER_SIZE;
			wanted_spec.callback = audio_callback;
			wanted_spec.userdata = this;

			if ((audio_deviceID = SDL_OpenAudioDevice(
				NULL,
				0,
				&wanted_spec,
				&spec,
				SDL_AUDIO_ALLOW_FREQUENCY_CHANGE | SDL_AUDIO_ALLOW_CHANNELS_CHANGE
			)) == 0) {
				av_log(NULL, AV_LOG_PANIC, "SDL_OpenAudioDevice: %s\n", SDL_GetError());
				av_log(NULL, AV_LOG_PANIC, "want freq %d channels %d\n", wanted_spec.freq, wanted_spec.channels);

				wanted_spec.channels = 2;
				if ((audio_deviceID = SDL_OpenAudioDevice(
					NULL,
					0,
					&wanted_spec,
					&spec,
					SDL_AUDIO_ALLOW_FREQUENCY_CHANGE | SDL_AUDIO_ALLOW_CHANNELS_CHANGE
				)) == 0) {
					av_log(NULL, AV_LOG_PANIC, "SDL_OpenAudioDevice: %s\n", SDL_GetError());
					av_log(NULL, AV_LOG_PANIC, "want freq %d channels %d\n", wanted_spec.freq, wanted_spec.channels);

					return -1;
				}
			}
		}

		AVDictionary *opts = NULL;
		av_dict_set(&opts, "threads", "auto", 0);

		if (avcodec_open2(codecCtx.get(), codec, &opts) < 0) {
			av_log(NULL, AV_LOG_PANIC, "Unsupported codec!\n");
			return -1;
		}
		av_dict_free(&opts);

		int64_t in_channel_layout = av_get_default_channel_layout(codecCtx->channels);
		int64_t out_channel_layout = av_get_default_channel_layout(spec.channels);
		SwrContext *a_convert_ctx = NULL;

		pFormatCtx->streams[stream_index]->discard = AVDISCARD_DEFAULT;
		switch (codecCtx->codec_type) {
		case AVMEDIA_TYPE_AUDIO:
			audioStream = stream_index;
			audio_st = pFormatCtx->streams[stream_index];
			audio_ctx = codecCtx;
			audio_buf_size = 0;
			audio_buf_index = 0;
			audio_clock_start = NAN;
			audio_clock = NAN;

			audio_out_sample_rate = spec.freq;
			audio_out_channels = spec.channels;

			audio_callback_time = (double)av_gettime() / 1000000.0;
			audio_eof = audio_eof_enum::playing;
			break;
		case AVMEDIA_TYPE_VIDEO:
			videoStream = stream_index;
			video_st = pFormatCtx->streams[stream_index];
			video_ctx = codecCtx;
			video_clock_start = NAN;

			frame_timer = (double)av_gettime() / 1000000.0;
			frame_last_delay.clear();
			frame_last_delay.push_back(40e-3);
			video_current_pts_time = av_gettime();
			
			{
				video_SAR = video_ctx->sample_aspect_ratio;
				double aspect_ratio = 0;
				if (video_ctx->sample_aspect_ratio.num == 0) {
					aspect_ratio = 0;
				}
				else {
					aspect_ratio = av_q2d(video_ctx->sample_aspect_ratio) *
						video_ctx->width / video_ctx->height;
				}
				if (aspect_ratio <= 0.0) {
					aspect_ratio = (double)video_ctx->width /
						(double)video_ctx->height;
				}
				video_height = codecCtx->height;
				video_width = ((int)rint(video_height * aspect_ratio)) & ~1;

				bool ret;
				if(screenauto)
					ret = SetScreenSize(video_width, video_height, video_width, video_height);
				else {
					ret = SetScreenSize(screenwidth, screenheight, video_width, video_height);
					screen->SetPosition(screenxpos, screenypos);
				}
				if (!ret)
					return -1;

				// initialize SWS context for software scaling
				sws_ctx = std::shared_ptr<SwsContext>(
					sws_getCachedContext(NULL,
						codecCtx->width, codecCtx->height,
						codecCtx->pix_fmt, video_width,
						video_height, AV_PIX_FMT_YUV420P,
						SWS_BICUBLIN, NULL, NULL, NULL
					), &sws_freeContext);
				video_srcheight = codecCtx->height;
				video_srcwidth = codecCtx->width;
			}
			video_eof = false;
			video_tid = SDL_CreateThread(video_thread, "video", this);
			break;
		case AVMEDIA_TYPE_SUBTITLE:
			subtitleStream = stream_index;
			subtitle_st = pFormatCtx->streams[stream_index];
			subtitle_ctx = codecCtx;
			subtitle_tid = SDL_CreateThread(subtitle_thread, "subtitle", this);
			break;
		default:
			pFormatCtx->streams[stream_index]->discard = AVDISCARD_ALL;
			break;
		}
		return 0;
	}

	void _FFplayer::stream_component_close(int stream_index)
	{
		AVFormatContext *ic = pFormatCtx.get();

		if (stream_index < 0 || (unsigned int)stream_index >= ic->nb_streams)
			return;

		AVCodecParameters *codecpar = ic->streams[stream_index]->codecpar;

		switch (codecpar->codec_type) {
		case AVMEDIA_TYPE_AUDIO:
			if (audio_deviceID > 0)
				SDL_CloseAudioDevice(audio_deviceID);
			audio_deviceID = 0;
			audioq.AbortQueue();
			break;
		case AVMEDIA_TYPE_VIDEO:
			videoq.AbortQueue();
			destory_pictures();
			if (video_tid > 0) {
				SDL_WaitThread(video_tid, NULL);
				video_tid = 0;
			}
			destory_pictures();
			break;
		case AVMEDIA_TYPE_SUBTITLE:
			subtitleq.AbortQueue();
			if (subtitle_tid > 0) {
				SDL_WaitThread(subtitle_tid, NULL);
				subtitle_tid = 0;
			}
			break;
		default:
			break;
		}

		ic->streams[stream_index]->discard = AVDISCARD_ALL;
		switch (codecpar->codec_type) {
		case AVMEDIA_TYPE_AUDIO:
			audio_st = NULL;
			audioStream = -1;
			break;
		case AVMEDIA_TYPE_VIDEO:
			video_st = NULL;
			videoStream = -1;
			break;
		case AVMEDIA_TYPE_SUBTITLE:
			subtitle_st = NULL;
			subtitleStream = -1;
			break;
		default:
			break;
		}
	}

	bool notfree = false;

	int _FFplayer::decode_thread(void *arg)
	{
		av_log(NULL, AV_LOG_INFO, "decode_thread start\n");
		SDL_SetThreadPriority(SDL_ThreadPriority::SDL_THREAD_PRIORITY_HIGH);
		_FFplayer *is = (_FFplayer *)arg;
		notfree = false;
		is->pFormatCtx = std::shared_ptr<AVFormatContext>(avformat_alloc_context(), [](AVFormatContext *ptr) { if(!notfree)avformat_close_input(&ptr); });
		AVFormatContext *pFormatCtx = is->pFormatCtx.get();
		AVPacket packet = { 0 };

		int video_index = -1;
		int audio_index = -1;
		int subtitle_index = -1;

		is->videoStream = -1;
		is->audioStream = -1;
		is->subtitleStream = -1;

		pFormatCtx->interrupt_callback.callback = decode_interrupt_cb;
		pFormatCtx->interrupt_callback.opaque = is;

		is->IOCtx = std::shared_ptr<AVIOContext>(avio_alloc_context(
			is->inputstream->getbuffer(),
			is->inputstream->getbuffersize(),
			0,
			is->inputstream.get(),
			is->inputstream->read_packet,
			NULL,
			is->inputstream->seek
		), &av_freep);
		pFormatCtx->pb = is->IOCtx.get();

		av_log(NULL, AV_LOG_VERBOSE, "avformat_open_input()\n");
		// Open video file
		if (avformat_open_input(&pFormatCtx, is->filename, NULL, NULL) != 0) {
			fprintf(stderr, "avformat_open_input() failed %s\n", is->filename);
			notfree = true;
			goto fail; // Couldn't open file
		}

		av_log(NULL, AV_LOG_VERBOSE, "avformat_find_stream_info()\n");
		//pFormatCtx->max_analyze_duration = 500000;
		// Retrieve stream information
		if (avformat_find_stream_info(pFormatCtx, NULL) < 0) {
			fprintf(stderr, "avformat_find_stream_info() failed %s\n", is->filename);
			goto fail; // Couldn't find stream information
		}
		
		av_log(NULL, AV_LOG_VERBOSE, "av_dump_format()\n");
		// Dump information about file onto standard error
		av_dump_format(pFormatCtx, 0, is->filename, 0);

		for(unsigned int stream_index = 0; stream_index<pFormatCtx->nb_streams; stream_index++)
			pFormatCtx->streams[stream_index]->discard = AVDISCARD_ALL;

		av_log(NULL, AV_LOG_VERBOSE, "av_find_best_stream()\n");
		// Find the first video and audio stream
		video_index = av_find_best_stream(pFormatCtx, AVMEDIA_TYPE_VIDEO, -1, -1, NULL, 0);
		audio_index = av_find_best_stream(pFormatCtx, AVMEDIA_TYPE_AUDIO, -1, -1, NULL, 0);
		subtitle_index = av_find_best_stream(pFormatCtx, AVMEDIA_TYPE_SUBTITLE, -1, (audio_index >= 0 ? audio_index : video_index), NULL, 0);
		if (audio_index >= 0) {
			av_log(NULL, AV_LOG_VERBOSE, "audio stream open()\n");
			is->stream_component_open(audio_index);
		}
		if (video_index >= 0) {
			av_log(NULL, AV_LOG_VERBOSE, "video stream open()\n");
			is->stream_component_open(video_index);
		}
		if (subtitle_index >= 0) {
			av_log(NULL, AV_LOG_VERBOSE, "subtitle stream open()\n");
			is->stream_component_open(subtitle_index);
		}
		is->external_clock = NAN;
		is->audio_only = false;
		is->video_only = false;

		if (is->videoStream < 0 || is->audioStream < 0) {
			if (is->videoStream < 0) {
				av_log(NULL, AV_LOG_VERBOSE, "video missing\n");
				is->video_eof = true;
				is->audio_only = true;
				is->display_on = true;

				bool ret1;
				if (is->screenauto)
					ret1 = is->SetScreenSize(640, 480, 0, 0);
				else {
					ret1 = is->SetScreenSize(is->screenwidth, is->screenheight, 0, 0);
					is->screen->SetPosition(is->screenxpos, is->screenypos);
				}
				if (!ret1)
					return -1;

				void *mem;
				int ret = is->getimagefunc(&mem);
				if (ret > 0) {
					std::shared_ptr<SDL_Surface> bmp = std::shared_ptr<SDL_Surface>(SDL_LoadBMP_RW(SDL_RWFromMem(mem, ret), 1), &SDL_FreeSurface);
					if(is->screenauto)
						ret1 = is->SetScreenSize(bmp->w, bmp->h, 0, 0);
					else {
						ret1 = is->SetScreenSize(is->screenwidth, is->screenheight, 0, 0);
						is->screen->SetPosition(is->screenxpos, is->screenypos);
					}
					is->screen->CreateStaticTexture(bmp.get());
					av_free(mem);
					if (!ret1) return -1;
				}
			}
			else {
				av_log(NULL, AV_LOG_VERBOSE, "audio missing\n");
				is->av_sync_type = AV_SYNC_VIDEO_MASTER;
				is->audio_eof = audio_eof_enum::eof;
				is->video_only = true;
			}
		}

		// main decode loop
		av_log(NULL, AV_LOG_INFO, "decode_thread read loop\n");
		bool error = false;
		is->startskip_internal = NAN;
		for (;;) {
			if (is->IsQuit()) {
				return 0;
			}
			if (!isnan(is->startskip)) {
				is->seek_pos = (int64_t)(is->startskip * AV_TIME_BASE);
				if (pFormatCtx->start_time != AV_NOPTS_VALUE)
					is->seek_pos += is->pFormatCtx->start_time;
				is->seek_rel = 0;
				is->seek_flags = 0;
				is->seek_req = true;
				is->startskip_internal = is->startskip;
				is->startskip = NAN;
			}
			// seek stuff goes here
			if (is->seek_req) {
				AVRational timebase = { 1, AV_TIME_BASE };
				av_log(NULL, AV_LOG_INFO, "stream seek request receive %.2f(%lld)\n", (double)(is->seek_pos) * av_q2d(timebase), is->seek_pos);
				int stream_index = -1;
				int64_t seek_target = is->seek_pos;
				int64_t seek_min = is->seek_rel > 0 ? seek_target - is->seek_rel + 2 : INT64_MIN;
				int64_t seek_max = is->seek_rel < 0 ? seek_target - is->seek_rel - 2 : INT64_MAX;

				if (is->videoStream >= 0 && DEFAULT_AV_SYNC_TYPE == AV_SYNC_VIDEO_MASTER) stream_index = is->videoStream;
				else if (is->audioStream >= 0 && DEFAULT_AV_SYNC_TYPE == AV_SYNC_AUDIO_MASTER) stream_index = is->audioStream;

				if (stream_index >= 0) {
					AVRational fixtimebase = pFormatCtx->streams[stream_index]->time_base;
					seek_target = av_rescale_q(seek_target, timebase, fixtimebase);
					seek_min = is->seek_rel > 0 ? seek_target - is->seek_rel + 2 : INT64_MIN;
					seek_max = is->seek_rel < 0 ? seek_target - is->seek_rel - 2 : INT64_MAX;
					timebase = fixtimebase;
				}
				av_log(NULL, AV_LOG_INFO, "stream seek min = %.2f target = %.2f max = %.2f\n", 
					seek_min*av_q2d(timebase), 
					seek_target*av_q2d(timebase),
					seek_max*av_q2d(timebase));
				int ret1 = avformat_seek_file(is->pFormatCtx.get(), stream_index,
					seek_min, seek_target, seek_max,
					is->seek_flags);
				if (ret1 < 0) {
					char buf[AV_ERROR_MAX_STRING_SIZE];
					char *errstr = av_make_error_string(buf, AV_ERROR_MAX_STRING_SIZE, ret1);
					av_log(NULL, AV_LOG_ERROR, "error avformat_seek_file() %d %s\n", ret1, errstr);
					error = true;
				}
				if (ret1 >=0) {
					is->pictq_active_serial = is->subpictq_active_serial = av_gettime();
					if (is->audioStream >= 0) {
						is->audio_eof = audio_eof_enum::playing;
						av_log(NULL, AV_LOG_INFO, "audio flush request\n");
						is->audioq.flush();
					}
					if (is->videoStream >= 0) {
						is->video_eof = false;
						av_log(NULL, AV_LOG_INFO, "video flush request\n");
						is->videoq.flush();
					}
					if (is->subtitleStream >= 0) {
						av_log(NULL, AV_LOG_INFO, "subtitle flush request\n");
						is->subtitleq.flush();
					}
				}
				if (is->seek_req_backorder) {
					is->seek_pos = is->seek_pos_backorder;
					is->seek_rel = is->seek_rel_backorder;
					is->seek_flags = is->seek_flags_backorder;
					is->seek_req_backorder = false;
				}
				else {
					is->seek_req = false;
				}
				if(is->overlay_remove_time == 0)
					is->overlay_remove_time = av_gettime();
			}

			if ((is->audioStream < 0 && is->videoq.size > MAX_VIDEOQ_SIZE) ||
				(is->videoStream < 0 && is->audioq.size > MAX_AUDIOQ_SIZE) ||
				(is->audioq.size > MAX_AUDIOQ_SIZE && is->videoq.size > MAX_VIDEOQ_SIZE)) {
				SDL_Delay(10);
				continue;
			}
			int ret1 = av_read_frame(is->pFormatCtx.get(), &packet);
			if (ret1 < 0) {
				av_packet_unref(&packet);
				if (ret1 == AVERROR(EAGAIN))
					continue;
				if ((ret1 == AVERROR_EOF) || (ret1 = AVERROR(EIO))) {
					if (error || is->pFormatCtx->pb->eof_reached) {
						if (ret1 == AVERROR_EOF) {
							av_log(NULL, AV_LOG_INFO, "decoder EOF\n");
						}
						else {
							av_log(NULL, AV_LOG_INFO, "decoder I/O Error\n");
						}
						if (is->videoStream >= 0) {
							av_log(NULL, AV_LOG_INFO, "video EOF request\n");
							is->videoq.putEOF();
						}
						if (is->audioStream >= 0) {
							av_log(NULL, AV_LOG_INFO, "audio EOF request\n");
							is->audioq.putEOF();
						}
						if (is->subtitleStream >= 0) {
							av_log(NULL, AV_LOG_INFO, "subtitle EOF request\n");
							is->subtitleq.putEOF();
						}

						while (!(is->IsQuit() || is->seek_req)) {
							SDL_Delay(100);
						}

						if (is->seek_req) continue;
						break;
					}
					error = true;
				}
				char buf[AV_ERROR_MAX_STRING_SIZE];
				char *errstr = av_make_error_string(buf, AV_ERROR_MAX_STRING_SIZE, ret1);
				av_log(NULL, AV_LOG_ERROR, "error av_read_frame() %d %s\n", ret1, errstr);
				continue;
			}
			error = false;
			if(isnan(is->external_clock))
				is->external_clock = av_gettime() / 1000000.0;
			// Is this a packet from the video stream?
			if (packet.stream_index == is->videoStream) {
				is->video_eof = false;
				is->videoq.put(&packet);
			}
			else if (packet.stream_index == is->audioStream) {
				is->audio_eof = audio_eof_enum::playing;
				is->audioq.put(&packet);
				if(!is->pause)
					SDL_PauseAudioDevice(is->audio_deviceID, 0);
				is->audio_pause = false;
			}
			else if (packet.stream_index == is->subtitleStream) {
				is->subtitleq.put(&packet);
			}
			else {
				av_packet_unref(&packet);
			}
		}
		/* all done - wait for it */
		while (!is->IsQuit()) {
			SDL_Delay(100);
		}

	fail:
		is->Quit();
		return 0;
	}

	void _FFplayer::stream_cycle_channel(int codec_type)
	{
		int start_index, old_index;
		int nb_streams = pFormatCtx->nb_streams;
		AVProgram *p = NULL;

		if (codec_type == AVMEDIA_TYPE_VIDEO) {
			start_index = old_index = videoStream;
		}
		else if (codec_type == AVMEDIA_TYPE_AUDIO) {
			start_index = old_index = audioStream;
		}
		else if (codec_type == AVMEDIA_TYPE_SUBTITLE) {
			start_index = old_index = subtitleStream;
		}
		else {
			return;
		}

		int stream_index = start_index;

		if (codec_type != AVMEDIA_TYPE_VIDEO && videoStream != -1) {
			p = av_find_program_from_stream(pFormatCtx.get(), NULL, videoStream);
			if (p) {
				nb_streams = p->nb_stream_indexes;
				for (start_index = 0; start_index < nb_streams; start_index++)
					if (p->stream_index[start_index] == stream_index)
						break;
				if (start_index == nb_streams)
					start_index = -1;
				stream_index = start_index;
			}
		}

		while (true) {
			if (++stream_index >= nb_streams)
			{
				if (codec_type == AVMEDIA_TYPE_SUBTITLE) {
					stream_index = -1;
					goto found;
				}
				if (start_index == -1)
				{
					if (p) p = NULL;
					else
						break;
				}
				stream_index = 0;
			}
			if (stream_index == start_index)
			{
				if (p) p = NULL;
				else
					break;
			}
			auto st = pFormatCtx->streams[p ? p->stream_index[stream_index] : stream_index];
			if (st->codecpar->codec_type == codec_type) {
				/* check that parameters are OK */
				switch (codec_type) {
				case AVMEDIA_TYPE_AUDIO:
					if (st->codecpar->sample_rate != 0 &&
						st->codecpar->channels != 0)
						goto found;
					break;
				case AVMEDIA_TYPE_VIDEO:
				case AVMEDIA_TYPE_SUBTITLE:
					goto found;
				default:
					break;
				}
			}
		}
		return;
	found:
		if (p && stream_index != -1)
			stream_index = p->stream_index[stream_index];

		if (old_index == stream_index) return;

		av_log(NULL, AV_LOG_INFO, "Stream Change %d -> %d\n", old_index, stream_index);

		if (codec_type == AVMEDIA_TYPE_VIDEO) {
			videoStream = -1;
		}
		else if (codec_type == AVMEDIA_TYPE_AUDIO) {
			audioStream = -1;
		}
		else if (codec_type == AVMEDIA_TYPE_SUBTITLE) {
			subtitleStream = -1;
		}
		else {
			return;
		}
		stream_component_close(old_index);
		if(stream_index >= 0)
			stream_component_open(stream_index);
		if (codec_type == AVMEDIA_TYPE_VIDEO) {
			videoStream = stream_index;
		}
		else if (codec_type == AVMEDIA_TYPE_AUDIO) {
			audioStream = stream_index;
		}
		else if (codec_type == AVMEDIA_TYPE_SUBTITLE) {
			subtitleStream = stream_index;
		}
		else {
			return;
		}

		// fix others
		if (codec_type == AVMEDIA_TYPE_VIDEO && videoStream != -1) {
			stream_cycle_channel(AVMEDIA_TYPE_AUDIO);
			stream_cycle_channel(AVMEDIA_TYPE_SUBTITLE);
		}
		char strbuf[64];
		sprintf_s(strbuf, "Stream Change %d -> %d", old_index, stream_index);
		overlay_text = strbuf;
		Redraw();
		overlay_remove_time = av_gettime() + 3 * 1000 * 1000;
		
		stream_seek((int64_t)((get_master_clock() - 0.5) * AV_TIME_BASE), 0);
	}

	void _FFplayer::stream_seek(int64_t pos, int rel)
	{
		if (!seek_req) {
			seek_pos = pos;
			seek_flags = 0;
			seek_rel = rel;
			seek_req = 1;
		}
		else {
			seek_pos_backorder = pos;
			seek_flags_backorder = 0;
			seek_rel_backorder = rel;
			seek_req_backorder = 1;
		}
		schedule_refresh(1);
	}

	void _FFplayer::SeekExternal(double pos) 
	{
		int64_t t = (int64_t)(pos * AV_TIME_BASE);

		if (pFormatCtx->start_time != AV_NOPTS_VALUE)
			t += pFormatCtx->start_time;
		stream_seek(t, 0);
	}

	void _FFplayer::seek_chapter(int incr) 
	{
		int64_t pos = (int64_t)(get_master_clock() * AV_TIME_BASE);
		unsigned int i;

		if (!pFormatCtx->nb_chapters)
			return;

		AVRational timebase = { 1, AV_TIME_BASE };
		/* find the current chapter */
		for (i = 0; i < pFormatCtx->nb_chapters; i++) {
			AVChapter *ch = pFormatCtx->chapters[i];
			if (av_compare_ts(pos, timebase, ch->start, ch->time_base) < 0) {
				i--;
				break;
			}
		}

		i += incr;
		i = FFMAX(i, 0);
		if (i >= pFormatCtx->nb_chapters)
			return;

		av_log(NULL, AV_LOG_INFO, "Seeking to chapter %d.\n", i);
		char strbuf[64];
		sprintf_s(strbuf, "Seeking to chapter %d", i);
		overlay_text = strbuf;
		stream_seek(av_rescale_q(pFormatCtx->chapters[i]->start, pFormatCtx->chapters[i]->time_base,
			timebase), 0);
	}

	void _FFplayer::overlay_txt(VideoPicture *vp) {
		overlay_txt(vp->pts);
	}

	double _FFplayer::get_duration()
	{
		if (pFormatCtx->duration) {
			return pFormatCtx->duration / 1000000.0;
		}
		else if (videoStream >= 0 && pFormatCtx->streams[videoStream]->duration) {
			return pFormatCtx->streams[videoStream]->duration / 1000000.0;
		}
		else if (audioStream >= 0 && pFormatCtx->streams[audioStream]->duration) {
			return pFormatCtx->streams[audioStream]->duration / 1000000.0;
		}
		else {
			return 0;
		}
	}

	void _FFplayer::overlay_txt(double pts)
	{
		if (!display_on && overlay_text.empty()) return;
		if (isnan(pts)) return;

		char out_text[1024];
		if (overlay_text.empty()) {
			double ns;
			int hh, mm, ss;
			int tns, thh, tmm, tss;
			pos_ratio = get_duration();
			tns = (int)pos_ratio;
			thh = tns / 3600;
			tmm = (tns % 3600) / 60;
			tss = (tns % 60);
			ns = pts;
			if (pFormatCtx->start_time != AV_NOPTS_VALUE) {
				ns -= pFormatCtx->start_time / 1000000.0;
			}
			else if (!isnan(video_clock_start))
				ns -= video_clock_start / 1000000.0;
			else if (!isnan(audio_clock_start))
				ns -= audio_clock_start / 1000000.0;
			pos_ratio = ns / pos_ratio;
			hh = (int)(ns) / 3600;
			mm = ((int)(ns) % 3600) / 60;
			ss = ((int)(ns) % 60);
			ns -= (int)(ns);
			if (1) {
				sprintf_s(out_text, "%2d:%02d:%02d.%03d/%2d:%02d:%02d",
					hh, mm, ss, (int)(ns * 1000), thh, tmm, tss);
			}
			else {
				sprintf_s(out_text, "%2d:%02d:%02d.%03d/%2d:%02d:%02d %.1f",
					hh, mm, ss, (int)(ns * 1000), thh, tmm, tss, video_delay_to_audio * 1000);
			}
		}
		SDL_SetRenderDrawColor(screen->renderer.get(), 32, 32, 255, 200);
		SDL_SetRenderDrawBlendMode(screen->renderer.get(), SDL_BlendMode::SDL_BLENDMODE_BLEND);
		SDL_Rect rect = { 0, screen->GetHight() - 50, screen->GetWidth() * pos_ratio, 50 };
		SDL_RenderFillRect(screen->renderer.get(), &rect);
		SDL_SetRenderDrawColor(screen->renderer.get(), 0, 0, 0, 255);
		SDL_SetRenderDrawBlendMode(screen->renderer.get(), SDL_BlendMode::SDL_BLENDMODE_NONE);

		SDL_Color textColor = { 0, 255, 0, 0 };
		std::shared_ptr<SDL_Surface> textSurface(TTF_RenderText_Solid(font.get(), (overlay_text.empty()) ? out_text : overlay_text.c_str(), textColor), &SDL_FreeSurface);
		std::shared_ptr<SDL_Texture> text(SDL_CreateTextureFromSurface(screen->renderer.get(), textSurface.get()), &SDL_DestroyTexture);
		int text_width = textSurface->w;
		int text_height = textSurface->h;
		if (text_width > screen->GetWidth() / 2) {
			double scale = screen->GetWidth() / 2.0 / text_width;
			text_width = (int)(scale * text_width);
			text_height = (int)(scale * text_height);
		}
		int x = (screen->GetWidth() - text_width) / 2;
		int y = screen->GetHight() - 30 - text_height;
		x -= x % 50;
		y -= y % 50;
		SDL_Rect renderQuad = { x, y, text_width, text_height };
		SDL_RenderCopy(screen->renderer.get(), text.get(), NULL, &renderQuad);
	}

	std::shared_ptr<SDL_Surface> _FFplayer::subtitle_ass(const char *text)
	{
		SDL_Color textColor = { 255, 255, 255, 0 };
		std::string txt(text);
		std::list<std::string> txtlist;
		std::string::size_type p, pos = 0;
		std::list<std::string> direction;

		if ((p = txt.find(':', pos)) == std::string::npos) return NULL;
		direction.push_back(txt.substr(pos, p - pos));
		pos = p + 1;

		for (int i = 0; i < 9; i++) {
			if ((p = txt.find(',', pos)) == std::string::npos) return NULL;
			direction.push_back(txt.substr(pos, p - pos));
			pos = p + 1;
		}
		txt = txt.substr(pos);
		p = pos = 0;
		for (size_t i = 0; i < txt.length(); i++) {
			if (txt[i] == '{' && i < txt.length() - 1 && txt[i + 1] == '\\') {
				// command
				pos = i+2;
				if ((p = txt.find('}', pos)) == std::string::npos) return NULL;
				std::string command = txt.substr(pos, p - pos - 1);
				i = p;
				pos = p + 1;
				continue;
			}
			if (txt[i] == '\\' && i < txt.length() - 1 && (txt[i + 1] == 'N' || txt[i + 1] == 'n')) {
				txtlist.push_back(txt.substr(pos, i - 1));
				pos = i + 2;
				i++;
				continue;
			}
		}
		txtlist.push_back(txt.substr(pos));

		std::list<std::shared_ptr<SDL_Surface>> txtSurfacelist;
		int h = 0, w = 0;
		for each (auto t in txtlist) {
			std::shared_ptr<SDL_Surface> txtsf(TTF_RenderUTF8_Blended_Wrapped(font.get(), t.c_str(), textColor, screen->subwidth), &SDL_FreeSurface);
			txtSurfacelist.push_back(txtsf);
			h += txtsf->h;
			w = (w > txtsf->w) ? w : txtsf->w;
		}
		Uint32 rmask, gmask, bmask, amask;
		rmask = 0x000000ff;
		gmask = 0x0000ff00;
		bmask = 0x00ff0000;
		amask = 0xff000000;
		std::shared_ptr<SDL_Surface> textSurface(SDL_CreateRGBSurface(0, w, h, 32, rmask, gmask, bmask, amask), &SDL_FreeSurface);
		SDL_FillRect(textSurface.get(), NULL, SDL_MapRGBA(textSurface->format, 0, 0, 0, 128));
		int xx = 0, yy = 0;
		for each (auto t in txtSurfacelist) {
			SDL_Rect rect = { xx, yy, t->w, t->h };
			SDL_BlitSurface(t.get(), NULL, textSurface.get(), &rect);
			yy += t->h;
		}
		return textSurface;
	}

	void _FFplayer::subtitle_display(double pts) 
	{
		std::shared_ptr<SubtitlePicture> sp;
		if (subpictq.peek(sp) == 0) {

			while (sp->serial < subpictq_active_serial && subpictq.get(sp) == 0)
				;
			if (sp->serial < subpictq_active_serial)
				return;

			if (pts > sp->pts + (double)sp->end_display_time / 1000)
				subpictq.get(sp);

			if (pts <= sp->pts + (double)sp->end_display_time / 1000 && 
				pts >= sp->pts + (double)sp->start_display_time / 1000) {

				if (sp->type == 0) {
					if (sp->serial != screen->subserial || screen->subwidth != screen->GetWidth()) {
						if (screen->subtitlelen != sp->numrects) {
							screen->subtitle.reset(new std::shared_ptr<SDL_Texture>[sp->numrects]);
							screen->subtitlelen = sp->numrects;
						}
						else {
							for (int i = 0; i < sp->numrects; i++) {
								int w = 0, h = 0;
								if(screen->subtitle[i].get() != NULL)
									SDL_QueryTexture(screen->subtitle[i].get(), NULL, NULL, &w, &h);
								if (w != sp->subrects[i]->w || h != sp->subrects[i]->h) {
									screen->subtitle[i] = std::shared_ptr<SDL_Texture>(
										SDL_CreateTexture(
											screen->renderer.get(),
											SDL_PIXELFORMAT_BGRA8888,
											SDL_TEXTUREACCESS_STREAMING,
											sp->subrects[i]->w,
											sp->subrects[i]->h),
										&SDL_DestroyTexture);
									SDL_SetTextureBlendMode(screen->subtitle[i].get(), SDL_BLENDMODE_BLEND);
								}
							}
						}
						for (int i = 0; i < sp->numrects; i++) {
							void *pixels = NULL;
							int pitch = 0;
							int h = sp->subrects[i]->h;
							SDL_LockTexture(screen->subtitle[i].get(), NULL, &pixels, &pitch);
							if (sp->subrects[i]->linesize[0] != pitch) {
								uint8_t *dst = (uint8_t*)pixels;
								int srcpitch = sp->subrects[i]->linesize[0];
								for (int y = 0; y < h; y++) {
									memcpy(dst, &sp->subrects[i]->data[0][srcpitch*y], pitch);
									dst += pitch;
								}
							}
							else {
								memcpy(pixels, sp->subrects[i]->data[0], pitch*h);
							}
							SDL_UnlockTexture(screen->subtitle[i].get());
						}
						screen->subserial = sp->serial;
						screen->subwidth = screen->GetWidth();
					}
					for (int i = 0; i < sp->numrects; i++) {
						int in_w = sp->subrects[i]->w;
						int in_h = sp->subrects[i]->h;
						int subw = sp->subw ? sp->subw : video_ctx->width;
						int subh = sp->subh ? sp->subh : video_ctx->height;
						int screenw = screen->GetWidth();
						int screenh = screen->GetHight();
						int out_w = screenw ? (in_w * screenw / subw) & ~1 : in_w;
						int out_h = screenh ? (in_h * screenh / subh) & ~1 : in_h;
						out_w = (out_w) ? out_w : subw;
						out_h = (out_h) ? out_h : subh;

						SDL_Rect rect = {
							(in_w) ? sp->subrects[i]->x * out_w / in_w : sp->subrects[i]->x,
							(in_h) ? sp->subrects[i]->y * out_h / in_h : sp->subrects[i]->y,
							out_w,
							out_h
						};
						SDL_RenderCopy(screen->renderer.get(), screen->subtitle[i].get(), NULL, &rect);
					}
				}
				else {
					if (screen->subtitlelen != sp->numrects) {
						screen->subtitle.reset(new std::shared_ptr<SDL_Texture>[sp->numrects]);
						screen->subtitlelen = sp->numrects;
					}
					if (sp->serial != screen->subserial || screen->subwidth != screen->GetWidth()) {
						screen->subserial = sp->serial;
						screen->subwidth = screen->GetWidth();
						int x = 0, y = 0;
						for (int i = 0; i < sp->numrects; i++) {
							if (sp->subrects[i]->text) {
								SDL_Color textColor = { 255, 255, 255, 0 };
								char *t = sp->subrects[i]->text;
								std::shared_ptr<SDL_Surface> textSurface(TTF_RenderUTF8_Blended_Wrapped(font.get(), t, textColor, screen->subwidth), &SDL_FreeSurface);
								screen->subtitle[i] = std::shared_ptr<SDL_Texture>(SDL_CreateTextureFromSurface(screen->renderer.get(), textSurface.get()), &SDL_DestroyTexture);
								sp->subrects[i]->x = x;
								sp->subrects[i]->y = y;
								sp->subrects[i]->w = textSurface->w;
								sp->subrects[i]->h = textSurface->h;
								y += sp->subrects[i]->h;
							}
							if (sp->subrects[i]->ass) {
								auto textSurface = subtitle_ass(sp->subrects[i]->ass);
								if (textSurface == NULL) continue;
								screen->subtitle[i] = std::shared_ptr<SDL_Texture>(SDL_CreateTextureFromSurface(screen->renderer.get(), textSurface.get()), &SDL_DestroyTexture);
								sp->subrects[i]->x = x;
								sp->subrects[i]->y = y;
								sp->subrects[i]->w = textSurface->w;
								sp->subrects[i]->h = textSurface->h;
								y += sp->subrects[i]->h;
							}
						}
						int height = screen->GetHight();
						for (int i = 0; i < sp->numrects; i++) {
							sp->subrects[i]->y += height - y;
						}
					}
					for (int i = 0; i < sp->numrects; i++) {
						SDL_Rect renderQuad = { sp->subrects[i]->x, sp->subrects[i]->y, sp->subrects[i]->w ,  sp->subrects[i]->h };
						SDL_RenderCopy(screen->renderer.get(), screen->subtitle[i].get(), NULL, &renderQuad);
					}
				}
			}
		}
	}

	void _FFplayer::video_display(VideoPicture *vp)
	{
		if (vp->allocated) {
			SDL_Rect rect;
			SDL_Rect rectsrc;
			double aspect_ratio;
			int w, h, x, y;

			rectsrc.x = 0;
			rectsrc.y = 0;
			rectsrc.w = vp->width;
			rectsrc.h = vp->height;

			if (video_ctx->sample_aspect_ratio.num == 0) {
				aspect_ratio = 0;
			}
			else {
				aspect_ratio = av_q2d(video_ctx->sample_aspect_ratio) *
					video_ctx->width / video_ctx->height;
			}
			if (aspect_ratio <= 0.0) {
				aspect_ratio = (double)video_ctx->width /
					(double)video_ctx->height;
			}
			SDL_LockMutex(screen_mutex.get());
			{
				screen->ShowWindow();
				h = screen->GetHight();
				w = ((int)rint(h * aspect_ratio)) & ~1;
				if (w > screen->GetWidth()) {
					w = screen->GetWidth();
					h = ((int)rint(w / aspect_ratio)) & ~1;
				}
				x = (screen->GetWidth() - w) / 2;
				y = (screen->GetHight() - h) / 2;

				rect.x = x;
				rect.y = y;
				rect.w = w;
				rect.h = h;

				void *pixels = NULL;
				int pitch = 0;
				SDL_LockTexture(screen->texture.get(), NULL, &pixels, &pitch);
				if (pitch != vp->bmp.linesize[0]) {
					int srcpitch = vp->bmp.linesize[0];
					for (int y = 0; y < vp->height; y++)
						memcpy((uint8_t *)pixels + pitch*y, &vp->bmp.data[0][y*srcpitch], pitch);
				}
				else {
					memcpy(pixels, vp->bmp.data[0], pitch*vp->height);
				}
				if (pitch / 2 != vp->bmp.linesize[2]) {
					int srcpitch = vp->bmp.linesize[2];
					uint8_t* dst = (uint8_t *)pixels + pitch*vp->height;
					for (int y = 0; y < vp->height / 2; y++)
						memcpy(dst + pitch*y / 2, &vp->bmp.data[2][y*srcpitch], pitch / 2);
				}
				else {
					memcpy((uint8_t *)pixels + pitch*vp->height, vp->bmp.data[2], pitch*vp->height / 4);
				}
				if (pitch / 2 != vp->bmp.linesize[1]) {
					int srcpitch = vp->bmp.linesize[1];
					uint8_t* dst = (uint8_t *)pixels + pitch*vp->height * 5 / 4;
					for (int y = 0; y < vp->height / 2; y++)
						memcpy(dst + pitch*y / 2, &vp->bmp.data[1][y*srcpitch], pitch / 2);
				}
				else {
					memcpy((uint8_t *)pixels + pitch*vp->height * 5 / 4, vp->bmp.data[1], pitch*vp->height / 4);
				}
				SDL_UnlockTexture(screen->texture.get());
				SDL_RenderClear(screen->renderer.get());
				SDL_RenderCopy(screen->renderer.get(), screen->texture.get(), NULL, &rect);
				if (subtitleStream >= 0)
					subtitle_display(vp->pts);
				overlay_txt(vp);
				SDL_RenderPresent(screen->renderer.get());
			}
			SDL_UnlockMutex(screen_mutex.get());
		}
	}

	VideoPicture *_FFplayer::next_picture_queue() 
	{
		pictq_prev = &pictq[pictq_rindex];
		/* update queue for next picture! */
		if (++pictq_rindex == VIDEO_PICTURE_QUEUE_SIZE) {
			pictq_rindex = 0;
		}
		SDL_LockMutex(pictq_mutex.get());
		pictq_size--;
		SDL_CondSignal(pictq_cond.get());
		SDL_UnlockMutex(pictq_mutex.get());
		return &pictq[pictq_rindex];
	}

	void _FFplayer::audioonly_video_display()
	{
		SDL_Rect rect;
		double aspect_ratio;
		int w, h, x, y;

		if (screen->statictexture != NULL) {
			SDL_QueryTexture(screen->statictexture, NULL, NULL, &w, &h);

			aspect_ratio = (double)w / (double)h;
			h = screen->GetHight();
			w = ((int)rint(h * aspect_ratio)) & -3;
			if (w > screen->GetWidth()) {
				w = screen->GetWidth();
				h = ((int)rint(w / aspect_ratio)) & -3;
			}
			x = (screen->GetWidth() - w) / 2;
			y = (screen->GetHight() - h) / 2;

			rect.x = x;
			rect.y = y;
			rect.w = w;
			rect.h = h;
		}
		SDL_LockMutex(screen_mutex.get());
		{
			screen->ShowWindow();
			SDL_RenderClear(screen->renderer.get());
			if (screen->statictexture != NULL)
				SDL_RenderCopy(screen->renderer.get(), screen->statictexture, NULL, &rect);

			overlay_txt(get_audio_clock());
			SDL_RenderPresent(screen->renderer.get());
		}
		SDL_UnlockMutex(screen_mutex.get());
	}

	void _FFplayer::set_clockduration(double pts)
	{
		duration = get_duration();
		double t = pts;
		if (pFormatCtx->start_time != AV_NOPTS_VALUE)
			t -= pFormatCtx->start_time / 1000000.0;
		else if (!isnan(video_clock_start))
			t -= video_clock_start / 1000000.0;
		else
			t -= audio_clock_start / 1000000.0;
		playtime = t;
		if (!isnan(stopduration) && playtime - (isnan(startskip_internal) ? 0 : startskip_internal) > stopduration)
			Quit();
	}

	void _FFplayer::Redraw() 
	{
		InterlockedIncrement(&force_draw);
		video_refresh_timer();
	}

	void _FFplayer::video_refresh_timer()
	{
		if (force_draw && video_st) {
			video_display(pictq_prev);
			InterlockedDecrement(&force_draw);
			return;
		}

		if (InterlockedDecrement(&remove_refresh) > 0) {
			return;
		}

		if (audio_only) {
			set_clockduration(get_audio_clock());
			audioonly_video_display();
			schedule_refresh(100);
			return;
		}
		if (!video_st) {
			schedule_refresh(250);
			return;
		}

		VideoPicture *vp = &pictq[pictq_rindex];
		while (vp->serial < pictq_active_serial && pictq_size > 0) {
			vp = next_picture_queue();
			frame_last_pts = 0;
		}

		if (seek_req) {
			schedule_refresh(250);
			pict_seek_after = true;
			frame_timer = av_gettime() / 1000000.0;
			return;
		}

		if (!video_only && (isnan(audio_clock) || (audio_eof != audio_eof_enum::eof && audio_pause))) {
			schedule_refresh(250);
			frame_timer = av_gettime() / 1000000.0;
			return;
		}

		if (pictq_size == 0) {
			schedule_refresh(1);
			return;
		}

		if (pause) {
			return;
		}

		while (pictq_size > 0)
		{
			if (pict_seek_after && av_sync_type != AV_SYNC_VIDEO_MASTER) {
				double ref_clock = get_master_clock();
				double diff = vp->pts - ref_clock;
				if (diff < 0) {
					vp = next_picture_queue();
					continue;
				}
			}

			pict_seek_after = false;
			video_current_pts = vp->pts;
			video_current_pts_time = av_gettime();
			set_clockduration(vp->pts);

			double delay = vp->pts - frame_last_pts; /* the pts from last time */
			if (delay >= -1.0 && delay <= 1.0) {
				for each(auto d in frame_last_delay)
				{
					delay += d;
				}
				delay /= (frame_last_delay.size() + 1);
			}
			else {
				/* if incorrect delay, use previous one */
				delay = 0;
				for each(auto d in frame_last_delay)
				{
					delay += d;
				}
				delay /= frame_last_delay.size();
			}
			/* save for next time */
			frame_last_pts = vp->pts;

			/* update delay to sync to audio if not master source */
			if (av_sync_type != AV_SYNC_VIDEO_MASTER) {
				double ref_clock = get_master_clock();
				double diff = vp->pts - ref_clock;

				diff += (audio_callback_time - av_gettime() / 1000000.0);
				video_delay_to_audio = diff;
				/* Skip or repeat the frame. Take delay into account
				FFPlay still doesn't "know if this is the best guess." */
				double sync_threshold = (delay > AV_SYNC_THRESHOLD) ? delay : AV_SYNC_THRESHOLD;
				if (fabs(diff) < AV_NOSYNC_THRESHOLD) {
					if (diff <= -sync_threshold) {
						delay = 0;
					}
					else if (diff >= sync_threshold * 2) {
						delay = (diff / sync_threshold + 1) * delay;
					}
					else if (diff > 0) {
						delay = (diff / sync_threshold) * delay;
					}
				}
			}
			if (delay >= -1.0 && delay <= 1.0) {
				frame_last_delay.push_back(delay);
				if (frame_last_delay.size() > 10)
					frame_last_delay.pop_front();
			}


			frame_timer += delay;
			const double skepdelay = 0.001;
			/* computer the REAL delay */
			double actual_delay = frame_timer - av_gettime() / 1000000.0;

			if (actual_delay > AV_SYNC_THRESHOLD) {
				schedule_refresh((int)(actual_delay * 1000) - 10);
			}
			else if (actual_delay > skepdelay) {
				schedule_refresh(1);
			}
			/* show the picture! */
			video_display(vp);
			vp = next_picture_queue();

			if (remove_refresh > 0)
				return;
		}
		if(remove_refresh == 0)
			schedule_refresh(1);
	}

	void _FFplayer::TogglePause()
	{
		pause = !pause;
		if (pause) {
			if (audio_deviceID != 0)
				SDL_PauseAudioDevice(audio_deviceID, 1);
		}
		else {
			Redraw();
			schedule_refresh(1);
			if (audio_deviceID != 0)
				SDL_PauseAudioDevice(audio_deviceID, 0);
		}
	}

	int _FFplayer::Play(Stream^ input, const char *name = NULL)
	{
		if (IsPlay) return -1;
		if (!Init()) return -1;
		IsPlay = true;
		EnterPlayer();
		
		mainthread = SDL_GetThreadID(NULL);

		if(screenwidth == 0 || screenheight == 0)
			screen->GetPosition(&screenxpos, &screenypos);
		screenwidth = (screenwidth <= 0) ? 640 : screenwidth;
		screenheight = (screenheight <= 0) ? 480 : screenheight;

		inputstream = std::shared_ptr<_Stream>(new _Stream(input, ct));
		if (name) {
			av_strlcpy(this->filename, name, strlen(name) + 1);
		}
		screen->title = name;

		remove_refresh = 0;

		schedule_refresh(40);

		parse_tid = SDL_CreateThread(decode_thread, "decode", this);
		if (!parse_tid) {
			return -1;
		}

		bool usercancel = EventLoop();
		usercancel = usercancel || ct->IsCancellationRequested;

		IsPlay = false;
		if (LeavePlayer()) {
			TTF_Quit();
			SDL_Quit();
		}
		return (usercancel)? 1: 0;
	}

	void _FFplayer::ToggleFullscreen()
	{
		ToggleFullscreen(!screen->fullscreen);
	}

	void _FFplayer::ToggleFullscreen(bool fullscreen)
	{
		IsFullscreen = fullscreen;
		SDL_LockMutex(screen_mutex.get());
		screen->SetFullScreen(IsFullscreen);
		SDL_UnlockMutex(screen_mutex.get());
		Redraw();
	}

	void _FFplayer::ResizeOriginal()
	{
		SetScreenSize(video_width, video_height, video_width, video_height);
		screenwidth = video_width;
		screenheight = video_height;
		screen->GetPosition(&screenxpos, &screenypos);
	}

	void _FFplayer::EventOnVolumeChange()
	{
		if (audio_volume > 100) audio_volume = 100;
		if (audio_volume < 0) audio_volume = 0;
		char strbuf[32];
		sprintf_s(strbuf, "Volume %3.0f%%", audio_volume);
		overlay_text = strbuf;
		Redraw();
		overlay_remove_time = av_gettime() + 3 * 1000 * 1000;
	}

	void _FFplayer::EventOnSeek(double value, bool frac, bool pre) 
	{
		double pos = get_master_clock();
		
		if (isnan(pos)) 
			pos = prev_pos;
		else 
			prev_pos = pos;

		int tns, thh, tmm, tss;
		int ns, hh, mm, ss;
		tns = 1;
		pos_ratio = get_duration();
		tns = (int)(pos_ratio);
		thh = tns / 3600;
		tmm = (tns % 3600) / 60;
		tss = (tns % 60);
		if (frac) {
			ns = (int)(value * tns);
		}
		else {
			pos += value;
			int64_t tmpns = (int64_t)pos;
			if (pFormatCtx->start_time != AV_NOPTS_VALUE)
				tmpns -= (int64_t)(pFormatCtx->start_time / 1000000.0);
			else
				tmpns -= (int64_t)(get_master_clock_start() / 1000000.0);
			ns = (int)(tmpns);
		}
		pos_ratio = ns / pos_ratio;
		hh = ns / 3600;
		mm = (ns % 3600) / 60;
		ss = (ns % 60);
		char strbuf[1024];
		if (frac) {
			sprintf_s(strbuf, "(%2.0f%%) %2d:%02d:%02d/%2d:%02d:%02d", value * 100,
				hh, mm, ss, thh, tmm, tss);
		}
		else {
			sprintf_s(strbuf, "(%+.0f sec) %2d:%02d:%02d/%2d:%02d:%02d", value,
				hh, mm, ss, thh, tmm, tss);
		}
		overlay_text = strbuf;
		overlay_remove_time = av_gettime() + 2 * 1000 * 1000;
		Redraw();
		if (pre) {
			if (frac) {
				av_log(NULL, AV_LOG_INFO, "Seek to %2.0f%% (%2d:%02d:%02d) of total duration(%2d:%02d:%02d)\n", value * 100,
					hh, mm, ss, thh, tmm, tss);
				int64_t ts = (int64_t)(value * pFormatCtx->duration);
				if (pFormatCtx->start_time != AV_NOPTS_VALUE)
					ts += pFormatCtx->start_time;
				stream_seek(ts, 0);
			}
			else {
				av_log(NULL, AV_LOG_INFO, "Seek to %.2f (%.2f)\n", pos, value);
				stream_seek((int64_t)(pos * AV_TIME_BASE), (int)(value));
			}
		}
	}

	int _FFplayer::_FFplayerFuncs::VoidFunction(SDL_Event &evnt)
	{
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::QuitTrueFunction(SDL_Event &evnt)
	{
		parent->Finalize();
		return 1;
	}

	int _FFplayer::_FFplayerFuncs::QuitFalseFunction(SDL_Event &evnt)
	{
		parent->Finalize();
		return -1;
	}

	int _FFplayer::_FFplayerFuncs::MouseMotionFunction(SDL_Event &evnt)
	{
		if (parent->cursor_hidden) {
			SDL_ShowCursor(1);
			parent->cursor_hidden = false;
		}
		parent->cursor_last_shown = av_gettime();
		if ((evnt.motion.state & SDL_BUTTON_LMASK) && (parent->display_on && evnt.motion.y > parent->screen->GetHight() - 50)) {
			frac = (double)evnt.motion.x / parent->screen->GetWidth();
			parent->EventOnSeek(frac, true, false);
			return 0;
		}
		if (!(evnt.motion.state & SDL_BUTTON_RMASK))
			return 0;
		if (parent->pFormatCtx->duration < 0) {
			return 0;
		}
		frac = (double)evnt.motion.x / parent->screen->GetWidth();
		parent->EventOnSeek(frac, true, false);
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::MouseButtonUpFunction(SDL_Event &evnt)
	{
		switch (evnt.button.button)
		{
		case SDL_BUTTON_LEFT:
			if (evnt.button.clicks == 2)
				parent->ToggleFullscreen();
			else {
				if (parent->display_on && evnt.button.y > parent->screen->GetHight() - 50) {
					frac = (double)evnt.button.x / parent->screen->GetWidth();
					parent->EventOnSeek(frac, true, true);
					break;
				}
			}
			parent->display_on = !parent->display_on;
			break;
		case SDL_BUTTON_RIGHT:
			frac = (double)evnt.button.x / parent->screen->GetWidth();
			parent->EventOnSeek(frac, true, true);
			break;
		default:
			break;
		}
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::WindowFunction(SDL_Event &evnt)
	{
		if (evnt.window.windowID == parent->screen->GetWindowID()) {
			switch (evnt.window.event)
			{
			case SDL_WINDOWEVENT_EXPOSED:
				parent->Redraw();
				break;
			case SDL_WINDOWEVENT_RESIZED:
				SDL_LockMutex(parent->screen_mutex.get());
				if(parent->IsFullscreen)
					parent->screen->SetScreenSize();
				else {
					parent->screen->SetScreenSize(evnt.window.data1, evnt.window.data2);
					parent->screenwidth = evnt.window.data1;
					parent->screenheight = evnt.window.data2;
					if(!parent->screenauto)
						parent->screen->SetPosition(parent->screenxpos, parent->screenypos);
				}
				SDL_UnlockMutex(parent->screen_mutex.get());
				break;
			case SDL_WINDOWEVENT_MOVED:
				parent->screen->GetPosition(&parent->screenxpos, &parent->screenypos);
				break;
			default:
				break;
			}
		}
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::KeyUpFunction(SDL_Event &evnt) 
	{
		auto f = KeyUpFuncs.find(evnt.key.keysym.sym);
		if (f == KeyUpFuncs.end()) return 0;
		return invoke(f->second, evnt);
	}

	int _FFplayer::_FFplayerFuncs::KeyDownFunction(SDL_Event &evnt)
	{
		auto f = KeyDownFuncs.find(evnt.key.keysym.sym);
		if (f == KeyDownFuncs.end()) return 0;
		return invoke(f->second, evnt);
	}

	int _FFplayer::_FFplayerFuncs::RefreshFunction(SDL_Event &evnt)
	{
		parent->video_refresh_timer();
		return 0;
	}

	void _FFplayer::_FFplayerFuncs::RegisterKeyFunction(
		SDL_Keycode key, 
		int(_FFplayer::_FFplayerFuncs::*keydownfnc)(SDL_Event &evnt), 
		int(_FFplayer::_FFplayerFuncs::*keyupfnc)(SDL_Event &evnt))
	{
		KeyUpFuncs[key] = keyupfnc;
		KeyDownFuncs[key] = keydownfnc;
	}

	int _FFplayer::_FFplayerFuncs::FunctionToggleFullscreen(SDL_Event &evnt) 
	{
		parent->ToggleFullscreen();
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::FunctionVolumeUp(SDL_Event &evnt)
	{
		parent->audio_volume++;
		parent->EventOnVolumeChange();
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::FunctionVolumeDown(SDL_Event &evnt)
	{
		parent->audio_volume--;
		parent->EventOnVolumeChange();
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::FunctionSeekPlus10sec(SDL_Event &evnt) 
	{
		incr = (evnt.key.repeat > 0) ? incr + 10.0 : 10.0;
		parent->EventOnSeek(incr, false, false);
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::FunctionSeekMinus10sec(SDL_Event &evnt)
	{
		incr = (evnt.key.repeat > 0) ? incr - 10.0 : -10.0;
		parent->EventOnSeek(incr, false, false);
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::FunctionSeekPlus60sec(SDL_Event &evnt)
	{
		incr = (evnt.key.repeat > 0) ? incr + 60.0 : 60.0;
		parent->EventOnSeek(incr, false, false);
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::FunctionSeekMinus60sec(SDL_Event &evnt)
	{
		incr = (evnt.key.repeat > 0) ? incr - 60.0 : -60.0;
		parent->EventOnSeek(incr, false, false);
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::FunctionSeekIncDone(SDL_Event &evnt)
	{
		parent->EventOnSeek(incr, false, true);
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::FunctionToggleDisplay(SDL_Event &evnt)
	{
		parent->display_on = !parent->display_on;
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::FunctionToggleMute(SDL_Event &evnt)
	{
		parent->audio_mute = !parent->audio_mute;
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::FunctionCycleChannel(SDL_Event &evnt)
	{
		parent->stream_cycle_channel(AVMEDIA_TYPE_VIDEO);
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::FunctionCycleAudio(SDL_Event &evnt)
	{
		parent->stream_cycle_channel(AVMEDIA_TYPE_AUDIO);
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::FunctionCycleSubtitle(SDL_Event &evnt)
	{
		parent->stream_cycle_channel(AVMEDIA_TYPE_SUBTITLE);
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::FunctionForwardChapter(SDL_Event &evnt)
	{
		parent->seek_chapter(1);
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::FunctionRewindChapter(SDL_Event &evnt)
	{
		parent->seek_chapter(-1);
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::FunctionTogglePause(SDL_Event &evnt)
	{
		parent->TogglePause();
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::FunctionResizeOriginal(SDL_Event &evnt)
	{
		parent->ResizeOriginal();
		return 0;
	}

	void _FFplayer::EventOnSrcVolumeChange()
	{
		char strbuf[64];
		if (audio_volume_auto)
			sprintf_s(strbuf, "Auto Volume %+ddB", audio_volume_dB);
		else
			sprintf_s(strbuf, "Volume %+ddB", audio_volume_dB);
		overlay_text = strbuf;
		Redraw();
		overlay_remove_time = av_gettime() + 3 * 1000 * 1000;
	}

	int _FFplayer::_FFplayerFuncs::FunctionSrcVolumeUp(SDL_Event &evnt)
	{
		parent->audio_volume_dB++;
		parent->EventOnSrcVolumeChange();
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::FunctionSrcVolumeDown(SDL_Event &evnt)
	{
		parent->audio_volume_dB--;
		parent->EventOnSrcVolumeChange();
		return 0;
	}

	int _FFplayer::_FFplayerFuncs::FunctionToggleDynNormalizeVolume(SDL_Event &evnt)
	{
		parent->audio_volume_auto = !parent->audio_volume_auto;
		parent->EventOnSrcVolumeChange();
		return 0;
	}

	bool _FFplayer::EventLoop()
	{
		std::map<Uint32, int(_FFplayer::_FFplayerFuncs::*)(SDL_Event &evnt)> MessageProcessor;

		MessageProcessor.insert(std::make_pair(SDL_QUIT, &_FFplayer::_FFplayerFuncs::QuitTrueFunction));
		MessageProcessor.insert(std::make_pair(FF_QUIT_EVENT, &_FFplayer::_FFplayerFuncs::QuitFalseFunction));
		MessageProcessor.insert(std::make_pair(FF_REFRESH_EVENT, &_FFplayer::_FFplayerFuncs::RefreshFunction));
		MessageProcessor.insert(std::make_pair(FF_INTERNAL_REFRESH_EVENT, &_FFplayer::_FFplayerFuncs::VoidFunction));
		MessageProcessor.insert(std::make_pair(SDL_MOUSEMOTION, &_FFplayer::_FFplayerFuncs::MouseMotionFunction));
		MessageProcessor.insert(std::make_pair(SDL_MOUSEBUTTONUP, &_FFplayer::_FFplayerFuncs::MouseButtonUpFunction));
		MessageProcessor.insert(std::make_pair(SDL_KEYUP, &_FFplayer::_FFplayerFuncs::KeyUpFunction));
		MessageProcessor.insert(std::make_pair(SDL_KEYDOWN, &_FFplayer::_FFplayerFuncs::KeyDownFunction));
		MessageProcessor.insert(std::make_pair(SDL_WINDOWEVENT, &_FFplayer::_FFplayerFuncs::WindowFunction));

		for each(auto keymap_base in defaultkeymap) {
			auto infunc = KeyFunctions.find(keymap_base.first);
			auto defaultfunc = keyfunctionlist.find(keymap_base.first);
			if (infunc == KeyFunctions.end()) {
				funcs.RegisterKeyFunction(keymap_base.second, defaultfunc->second.first, defaultfunc->second.second);
			}
			else {
				for (; infunc != KeyFunctions.end(); ++infunc) {
					funcs.RegisterKeyFunction(infunc->second, defaultfunc->second.first, defaultfunc->second.second);
				}
			}
		}

		cursor_last_shown = av_gettime(); 
		while (true) {
			SDL_Event event;
			int ret;

			if (!cursor_hidden || overlay_remove_time > 0) {
				double delay1 = ((cursor_hidden)? 0: CURSOR_HIDE_DELAY - (av_gettime() - cursor_last_shown)) / 1000.0;
				delay1 = (delay1 < 0) ? 0 : delay1;
				double delay2 = ((overlay_remove_time > 0) ? overlay_remove_time - av_gettime() : 0) / 1000.0;
				delay2 = (delay2 < 0) ? 0 : delay2;
				double delay = (delay1 > delay2) ? delay2 : delay1;

				SDL_AddTimer(int(delay), sdl_internal_refresh_timer_cb, this);
			}

			SDL_WaitEvent(&event);

			if (!cursor_hidden && av_gettime() - cursor_last_shown > CURSOR_HIDE_DELAY) {
				SDL_ShowCursor(0);
				cursor_hidden = true;
			}
			if (overlay_remove_time > 0 && overlay_remove_time < av_gettime()) {
				overlay_remove_time = 0;
				overlay_text.erase();
			}

			auto f = MessageProcessor.find(event.type);
			if (f == MessageProcessor.end()) continue;
			ret = funcs.invoke(f->second, event);
			if (ret == 0) continue;
			return (ret > 0);
		}
	}

	void _FFplayer::Quit()
	{
		if (!IsPlay) return;
		quit = true;

		SDL_Event event;
		memset(&event, 0, sizeof(event));
		event.type = FF_QUIT_EVENT;
		event.user.data1 = this;
		SDL_PushEvent(&event);
	}
	
	////////////////////////////////////////////////////////////////////////////////////////////

	static char buf[4096] = { 0 };
	static char prevbuf[4096] = { 0 };
	static int dupcount = 0;
	gcroot<TextWriter^> outstream;

	extern "C" void my_log_callback(void *ptr, int level, const char *fmt, va_list vargs)
	{
		if (level > AV_LOG_INFO) return;
		if (static_cast<TextWriter^>(outstream) != nullptr) {
			WaitForSingleObject(hLogMutex, INFINITE);

			vsnprintf_s(buf, sizeof(buf), fmt, vargs);
			if (strcmp(buf, prevbuf) == 0) {
				dupcount++;
			}
			else {
				if (dupcount > 0) {
					sprintf_s(prevbuf, "Last message repeated %d times\n", dupcount);
					auto strbyte = gcnew array<Byte>((int)strlen(prevbuf));
					for (int i = 0; i < strbyte->Length; i++) {
						strbyte[i] = prevbuf[i];
					}
					auto str = System::Text::Encoding::UTF8->GetString(strbyte);
					outstream->WriteLine(str);
					outstream->Flush();
				}
				strcpy_s(prevbuf, buf);
				dupcount = 0;
				auto strbyte = gcnew array<Byte>((int)strlen(buf));
				for (int i = 0; i < strbyte->Length; i++) {
					strbyte[i] = buf[i];
				}
				auto str = System::Text::Encoding::UTF8->GetString(strbyte);
				outstream->WriteLine(str);
				outstream->Flush();
			}

			ReleaseMutex(hLogMutex);
		}
	}

	///////////////////////////////////////////////////////////////////////////////////////////

	delegate int CppGetImageDelegate(void**);

	public ref class FFplayer : public IDisposable {
	public:
		Object^	Tag;
		System::Threading::CancellationToken ct;

		FFplayer() {
			this->player = new _FFplayer();
			cppgetimagefunc = gcnew CppGetImageDelegate(this, &FFplayer::ConvertImage);
			auto ptr = Marshal::GetFunctionPointerForDelegate(cppgetimagefunc);
			this->player->getimagefunc = (_FFplayer::CallBackGetImageProc)ptr.ToPointer();
		}
		virtual ~FFplayer() {//デストラクタ
			if (this && this->player) {
				this->player->Quit();
				delete this->player;
				this->player = NULL;
			}
		}
		!FFplayer() {//ファイナライザ
			this->~FFplayer();
		}
		int Play(Stream^ input) {
			return this->player->Play(input);
		}
		int Play(Stream^ input, String^ name) 
		{
			array<Byte>^ encodedBytes = System::Text::Encoding::UTF8->GetBytes(name);
			pin_ptr<Byte> pinnedBytes = &encodedBytes[0];
			return this->player->Play(input, (const char*)pinnedBytes);
		}
		int Play(Stream^ input, String^ name, System::Threading::CancellationToken^ ct)
		{
			this->ct = *ct;
			if (this->ct.IsCancellationRequested) return -1;
			this->player->ct = ct;
			return Play(input, name);
		}

		void Stop() 
		{
			this->player->Quit();
			this->ct = System::Threading::CancellationToken::None;
		}

		void SetKeyFunctions(System::Collections::Generic::Dictionary<FFplayerKeymapFunction, array<System::Windows::Forms::Keys>^>^ keymap)
		{
			this->player->KeyFunctions.clear();
			for each (System::Collections::Generic::KeyValuePair<FFplayerKeymapFunction, array<System::Windows::Forms::Keys>^> item in keymap)
			{
				_FFplayer::FFplayer_KeyCommand cmd = (_FFplayer::FFplayer_KeyCommand)(int)item.Key;
				for each(auto akey in item.Value) {
					SDL_Keycode key = GetKeycode(akey);
					this->player->KeyFunctions.insert(std::make_pair(cmd, key));
				}
			}
		}

		void SetLogger(TextWriter^ output) {
			outstream = output;
		}

		property bool Mute {
			bool get() {
				return player->audio_mute;
			}
			void set(bool value) {
				player->audio_mute = value;
			}
		}

		property bool Display {
			bool get() {
				return player->display_on;
			}
			void set(bool value) {
				player->display_on = value;
			}
		}

		property double Volume {
			double get() {
				return player->audio_volume;
			}
			void set(double value) {
				player->audio_volume = value;
			}
		}

		property bool Fullscreen {
			bool get() {
				return player->GetFullscreen();
			}
			void set(bool value) {
				player->SetFullscreen(value);
			}
		}

		property bool FullscreenState {
			bool get() {
				return player->GetFullscreenState();
			}
		}

		property GetImageDelegate^ GetImageFunc {
			GetImageDelegate^ get() {
				return getimagefunc;
			}
			void set(GetImageDelegate^ value) {
				getimagefunc = value;
			}
		}

		property String^ FontPath {
			String^ get() {
				return gcnew String(this->player->fontfile.c_str());
			}
			void set(String^ value) {
				if (value == nullptr) return;
				if (value->Length == 0) {
					this->player->fontfile = "";
					return;
				}
				array<Byte>^ Bytes = System::Text::Encoding::UTF8->GetBytes(value);
				pin_ptr<Byte> pinnedBytes = &Bytes[0];
				this->player->fontfile = (char *)pinnedBytes;
			}
		}

		property int FontSize {
			int get() {
				return this->player->fontsize;
			}
			void set(int value) {
				this->player->fontsize = value;
			}
		}

		property double Duration {
			double get() {
				return this->player->duration;
			}
		}

		property double PlayTime {
			double get() {
				return this->player->playtime;
			}
			void set(double value) {
				this->player->SeekExternal(value);
			}
		}

		property double StartSkip {
			double get() {
				return this->player->startskip;
			}
			void set(double value) {
				this->player->startskip = value;
			}
		}

		property double StopDuration {
			double get() {
				return this->player->stopduration;
			}
			void set(double value) {
				this->player->stopduration = value;
			}
		}

		property bool ScreenAuto {
			bool get() {
				return this->player->screenauto;
			}
			void set(bool value) {
				this->player->screenauto = value;
			}
		}

		property int ScreenWidth {
			int get() {
				return this->player->screenwidth;
			}
			void set(int value) {
				this->player->screenwidth = value;
			}
		}

		property int ScreenHeight {
			int get() {
				return this->player->screenheight;
			}
			void set(int value) {
				this->player->screenheight = value;
			}
		}

		property int ScreenXPos {
			int get() {
				return this->player->screenxpos;
			}
			void set(int value) {
				this->player->screenxpos = value;
			}
		}

		property int ScreenYPos {
			int get() {
				return this->player->screenypos;
			}
			void set(int value) {
				this->player->screenypos = value;
			}
		}

	private:
		_FFplayer* player;
		GetImageDelegate^ getimagefunc;
		CppGetImageDelegate^ cppgetimagefunc;

		int ConvertImage(void **mem) {
			if (getimagefunc == nullptr) return 0;

			System::Drawing::Bitmap^ bmp = getimagefunc(this);
			if (bmp == nullptr) return 0;
			auto ms = gcnew MemoryStream();
			bmp->Save(ms, System::Drawing::Imaging::ImageFormat::Bmp);

			int len = (int)ms->Length;
			if (len > 0) {
				ms->Position = 0;
				array<Byte>^ buffer = gcnew array<Byte>(len);
				ms->Read(buffer, 0, len);

				pin_ptr<Byte> p = &buffer[0];
				*mem = av_malloc(len);
				memcpy(*mem, p, len);
			}
			return len;
		}
	};


}
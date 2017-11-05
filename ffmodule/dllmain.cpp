// dllmain.cpp : Defines the entry point for the DLL application.
#include "stdafx.h"
#include <tchar.h>
#include <SDL.h>

extern "C" {
#include <libavcodec/avcodec.h>
}

AVPacket flush_pkt;
AVPacket eof_pkt;
AVPacket abort_pkt;

HANDLE hPlayEvent;
unsigned int PlayerCount = 0;
HANDLE hLogMutex;

extern "C" extern void my_log_callback(void *ptr, int level, const char *fmt, va_list vargs);

static const char *FLUSH_STR = "FLUSH";
static const char *EOF_STR = "EOF";
static const char *ABORT_STR = "ABORT";

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
					 )
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
		av_init_packet(&flush_pkt);
		av_packet_from_data(&flush_pkt, (uint8_t *)FLUSH_STR, (int)strlen(FLUSH_STR));
		av_init_packet(&eof_pkt);
		av_packet_from_data(&eof_pkt, (uint8_t *)EOF_STR, (int)strlen(EOF_STR));
		av_init_packet(&abort_pkt);
		av_packet_from_data(&abort_pkt, (uint8_t *)ABORT_STR, (int)strlen(ABORT_STR));
		PlayerCount = 0;
		hPlayEvent = CreateEvent(NULL, TRUE, TRUE, _T("ffmodule_playstop"));
		hLogMutex = CreateMutex(NULL, FALSE, _T("ffmodule_logMutex"));

		av_log_set_callback(my_log_callback);
		break;
	case DLL_THREAD_ATTACH:
	case DLL_THREAD_DETACH:
		break;
	case DLL_PROCESS_DETACH:
		while(PlayerCount > 0)
			WaitForSingleObject(hPlayEvent, INFINITE);
		CloseHandle(hPlayEvent);
		CloseHandle(hLogMutex);
		break;
	}
	return TRUE;
}

void EnterPlayer() 
{
	InterlockedIncrement(&PlayerCount);
}

bool LeavePlayer()
{
	unsigned int ret = InterlockedDecrement(&PlayerCount);
	SetEvent(hPlayEvent);
	return ret == 0;
}
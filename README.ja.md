# TSviewCloud
TS viewer for Cloud Drive

## Overview
メディアファイルをクラウドストレージからそのままストリーミング再生するソフト。
暗号化したままでもOK

## Description
* 内蔵のFFmpegプレーヤでメディアファイルを再生できます。
* MPEG2 TSファイルについては、udp送信してTVtestやVNC playerなどの外部プレーヤーでも再生できます。
* クラウドストレージ上へのファイルのアップロードやダウンロード、リネームや移動などの基本操作が可能です。
* 対応しているクラウドストレージ: Amazon drive, Google drive, Local filesystem.
* [CarotDAV](http://www.rei.to/carotdav.html "CarotDAV") と [rclone](https://rclone.org/ "rclone") の暗号化方式に対応しています。
* 暗号化されたファイルも、クラウドから直接ストリーミング再生できます。

## ScreenShots

## Requirement
このプログラムは Visual Studio 2017 C++ でコンパイルされています。
Visual Studio 2017 の Microsoft Visual C++ 再頒布可能パッケージが必要です。
インストールする方のバージョンを選んでください。
* 64bit(x64)
<https://go.microsoft.com/fwlink/?LinkId=746572>
* 32bit(x86)
<https://go.microsoft.com/fwlink/?LinkId=746571>

このプログラムは c# .NET 4.7 でコンパイルされています。
システムにインストールされていない場合は、Microsoft .NET Framework 4.7.1が必要です。
runtime.
<http://go.microsoft.com/fwlink/?LinkId=852095>

## License
### TSviewCloud
CC0 です。ご自由にご利用ください。

[![CC0](http://i.creativecommons.org/p/zero/1.0/88x31.png "CC0")](http://creativecommons.org/publicdomain/zero/1.0/deed.en)

### FFmpeg
  LGPLv2.1

### SDL.dll(SDL 2.0)
  zlib license

### SDL_ttf
  zlib license

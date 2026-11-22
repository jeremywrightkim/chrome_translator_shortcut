# 번역 단축키

**한국어** | [English](README.en.md)

크롬에서 단축키 하나(기본 `Alt+T`)로 **크롬 기본 페이지 번역**을 켜고 끄는 Windows 트레이 프로그램입니다. 크롬 자체 번역을 그대로 쓰므로 결과는 크롬 번역과 같습니다.

## 다운로드와 사용법

1. [Releases](https://github.com/jeremywrightkim/chrome_translator_shortcut/releases/latest)에서 `TranslatorShortcut.exe`를 받아 실행합니다. 설치는 필요 없습니다.
   - SmartScreen 경고가 뜨면 [추가 정보] → [실행]을 누르세요. (코드 서명이 없어서 뜨는 경고입니다)
2. 크롬에서 `Alt+T`를 누르면 번역되고, 다시 누르면 원문으로 돌아옵니다.

- 번역할 언어는 크롬 설정을 따릅니다 (`chrome://settings/languages`).
- 단축키는 크롬 창이 맨 앞에 있을 때만 동작하므로, 다른 프로그램에서는 같은 키를 그대로 쓸 수 있습니다.
- 트레이 아이콘을 두 번 클릭하거나 exe를 한 번 더 실행하면 환경설정 창이 열립니다. 단축키 변경, Windows 시작 시 자동 실행을 설정할 수 있습니다.

요구 사항: Windows 10 또는 11

### 단축키 고르기

크롬이나 웹페이지가 쓰는 키를 지정하면 그 키의 원래 기능이 막힙니다.

- **권장:** 기본값 `Alt+T` (왼손으로 누르기 쉽고 크롬과 겹치지 않음)
- **키 하나로 쓰려면:** `F4`, `F9`, `F8`, `F2` (크롬이 쓰지 않는 키. 단, `F2`·`F4`는 구글 스프레드시트, `F8`·`F9`는 개발자 도구와 겹침)
- **피할 키:** `Ctrl`+글자, 오른쪽 `Alt`(한/영 키), `F1`, `F3`, `F5`, `F6`, `F7`, `F10`, `F11`, `F12`

## 단축키가 동작하지 않을 때

기본 설정은 **한국어 크롬 154** 기준입니다. 크롬 버전이나 언어가 다르면 환경설정에서 다음을 해 보세요.

1. **[호환성 검사]:** 크롬을 건드리지 않고 번역 차단 정책, Google 번역 설정, 화면 요소를 확인합니다.
2. **[보정…]:** 외국어 페이지를 열어 두고, 안내에 따라 번역 아이콘과 번역 창의 버튼을 직접 클릭하면 사용자 환경에 맞게 다시 인식합니다.

그래도 안 되면 트레이 메뉴 → [로그 폴더 열기]의 `log.txt`를 첨부해 [이슈](https://github.com/jeremywrightkim/chrome_translator_shortcut/issues)로 알려 주세요.

## 빌드

별도 SDK 없이 Windows에 들어 있는 C# 컴파일러로 빌드합니다. 결과물은 `bin\TranslatorShortcut.exe`입니다.

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

## 후원

도움이 되셨다면 [후원 페이지](https://cgcg.review/support/)에서 응원해 주세요. 트레이 메뉴의 [후원하기…]로도 열 수 있습니다.

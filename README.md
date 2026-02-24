# Emuera for Linux (Ported)

This project is a Linux port of the original [Emuera](https://gitlab.com/VVIIlet/emuera) based on .NET.

> **Original Project:** [https://gitlab.com/VVIIlet/emuera](https://gitlab.com/VVIIlet/emuera)  
> **Original Author:** MinorShift & VVIIlet

## License Notice

- Emuera: Original Copyright (C) 2008- MinorShift.
- This repository contains a **modified version** of the original Emuera source.
- LibWebp: Copyright (c) 2010, Google Inc.
- Keep license files when redistributing source/binaries:
- `Readme/License/Emuera.LICENSE.txt`
- `Readme/License/LibWebp.LICENSE.txt`

## Run the Program (End Users)

Use these commands in a deployed game directory (example: `eraTWKR`):

1. Grant execute permission once:
`chmod +x Run-Emuera-Linux.sh`
2. Start the game:
`./Run-Emuera-Linux.sh`
3. Optional runtime checks:
`./Run-Emuera-Linux.sh --run-smoke-only`

## Build and Deploy (Developers)

Use these commands in this repository root (`emuera.em`):

1. Publish Linux CLI binary:
`./scripts/publish-linux-cli.sh`
2. Deploy into a game directory:
`./scripts/deploy-linux-standalone.sh /path/to/game "Emuera1824+v11+webp+test+fix.exe"`
3. Run from the game directory:
`./Run-Emuera-Linux.sh`

## Notes

- This project is a porting/modification work and is not the original upstream project.
- Please keep original copyright and license notices.

---

# Emuera for Linux (포팅 버전)

이 프로젝트는 원본 [Emuera](https://gitlab.com/VVIIlet/emuera)를 .NET 기반으로 리눅스에서 동작하도록 포팅한 버전입니다.

> **원본 프로젝트:** [https://gitlab.com/VVIIlet/emuera](https://gitlab.com/VVIIlet/emuera)  
> **원작자:** MinorShift & VVIIlet

## 라이선스 안내

- Emuera: Original Copyright (C) 2008- MinorShift.
- 이 저장소는 원본 Emuera 소스를 **수정한 버전**을 포함합니다.
- LibWebp: Copyright (c) 2010, Google Inc.
- 소스/바이너리 재배포 시 아래 라이선스 파일을 유지해야 합니다.
- `Readme/License/Emuera.LICENSE.txt`
- `Readme/License/LibWebp.LICENSE.txt`

## 프로그램 실행 방법 (사용자)

배포된 게임 폴더(예: `eraTWKR`)에서 아래처럼 실행합니다.

1. 1회 실행 권한 부여:
`chmod +x Run-Emuera-Linux.sh`
2. 게임 실행:
`./Run-Emuera-Linux.sh`
3. 선택 사항(스모크 체크):
`./Run-Emuera-Linux.sh --run-smoke-only`

## 빌드 및 배포 방법 (개발자)

이 저장소 루트(`emuera.em`)에서 아래 명령을 사용합니다.

1. 리눅스 CLI 바이너리 퍼블리시:
`./scripts/publish-linux-cli.sh`
2. 게임 폴더로 배포:
`./scripts/deploy-linux-standalone.sh /path/to/game "Emuera1824+v11+webp+test+fix.exe"`
3. 게임 폴더에서 실행:
`./Run-Emuera-Linux.sh`

## 참고

- 이 프로젝트는 원본 프로젝트 자체가 아니라 포팅/수정 작업 결과물입니다.
- 원본 저작권 및 라이선스 고지는 반드시 유지해 주세요.

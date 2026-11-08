Unicode True
!define APPNAME "Kye"          ; change per game
!define EXENAME "Launcher.exe"

!include "MUI2.nsh"

Name "${APPNAME}"
OutFile "${APPNAME}-Setup.exe"
InstallDir "$LOCALAPPDATA\${APPNAME}"
RequestExecutionLevel user        ; no admin / UAC
SetCompressor /SOLID lzma

!define MUI_ICON "app.ico"
!define MUI_UNICON "app.ico"

; Finish page: "Run" plus (repurposed checkbox) optional desktop shortcut
!define MUI_FINISHPAGE_RUN "$INSTDIR\${EXENAME}"
!define MUI_FINISHPAGE_SHOWREADME ""
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Create a desktop shortcut"
!define MUI_FINISHPAGE_SHOWREADME_FUNCTION CreateDesktopShortcut

!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Function CreateDesktopShortcut
  CreateShortcut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\${EXENAME}"
FunctionEnd

Section "Install"
  SetOutPath "$INSTDIR"
  File "${EXENAME}"
  File "settings.ini"
  File /r "app"

  CreateShortcut "$SMPROGRAMS\${APPNAME}.lnk" "$INSTDIR\${EXENAME}"

  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "DisplayName" "${APPNAME}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "DisplayIcon" "$INSTDIR\${EXENAME}"
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "NoModify" 1
SectionEnd

Section "Uninstall"
  Delete "$SMPROGRAMS\${APPNAME}.lnk"
  Delete "$DESKTOP\${APPNAME}.lnk"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}"

  Delete "$INSTDIR\${EXENAME}"
  Delete "$INSTDIR\settings.ini"
  Delete "$INSTDIR\launcher.log"
  Delete "$INSTDIR\Uninstall.exe"

  MessageBox MB_YESNO "Also delete the game files in the 'app' folder?$\n$\nThis includes any exported levels stored there." IDNO skip
    RMDir /r "$INSTDIR\app"
  skip:
  RMDir "$INSTDIR"   ; only removes the folder if empty
SectionEnd

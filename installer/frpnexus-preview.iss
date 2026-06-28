#define MyAppName "FrpNexus"
#define MyAppPublisher "Arturia"
#define MyAppExeName "Arturia.FrpNexus.Desktop.exe"

#ifndef VelopackSetup
#error "VelopackSetup path must be defined"
#endif

#ifndef AppVersion
#define AppVersion "0.4.0-preview.3"
#endif

#ifndef SetupIcon
#define SetupIcon "src\Arturia.FrpNexus.Desktop\Assets\frpnexus-logo.ico"
#endif

#ifndef OutputDir
#define OutputDir "artifacts\release"
#endif

#ifndef OutputBase
#define OutputBase "FrpNexus-Setup-" + AppVersion
#endif

#define VelopackFileName RemoveBackslashUnlessRoot(ExtractFileName(VelopackSetup))

[Setup]
AppId={{A7E3C6D2-8F1B-4B6A-9D2E-1C5F8A3B7E90}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Arturia\FrpNexus
DisableDirPage=no
DefaultGroupName=FrpNexus
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBase}
SetupIconFile={#SetupIcon}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
Uninstallable=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#VelopackSetup}"; DestDir: "{tmp}"; Flags: ignoreversion deleteafterinstall; DestName: "VelopackSetup.exe"

[Code]
function IsExistingVelopackInstall: Boolean;
var
  ExistingInstallPath: String;
begin
  ExistingInstallPath := ExpandConstant('{localappdata}\Arturia.FrpNexus');
  Result := DirExists(ExistingInstallPath);
end;

function InitializeSetup: Boolean;
begin
  if IsExistingVelopackInstall then
  begin
    MsgBox(
      '检测到 FrpNexus 已安装。请先通过 Windows“应用和功能”或现有 FrpNexus 卸载入口卸载旧版后，再重新运行本安装向导选择新的安装目录。',
      mbError,
      MB_OK);
    Result := False;
  end
  else
  begin
    Result := True;
  end;
end;

procedure InitializeWizard;
begin
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  SetupPath: String;
  SetupParameters: String;
begin
  if CurStep = ssPostInstall then
  begin
    SetupPath := ExpandConstant('{tmp}\VelopackSetup.exe');
    SetupParameters := '--installto "' + ExpandConstant('{app}') + '"';

    WizardForm.StatusLabel.Caption := '正在安装 FrpNexus...';
    if not Exec(SetupPath, SetupParameters, '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    begin
      MsgBox('无法启动 FrpNexus 内层安装器。安装已停止。', mbError, MB_OK);
      Abort;
    end;

    if ResultCode <> 0 then
    begin
      MsgBox(
        'FrpNexus 内层安装器未成功完成。请确认旧版已卸载，然后重新运行本安装向导。退出码：' + IntToStr(ResultCode),
        mbError,
        MB_OK);
      Abort;
    end;
  end;
end;

#define AppName "TrainingDeskCalendar"
#define AppDisplayName "训练桌历"
#ifndef AppVersion
#define AppVersion "0.1.0"
#endif
#define AppExecutable "TrainingDeskCalendar.App.exe"

[Setup]
AppId={{50D83759-8D5B-4F74-8BD7-C23C04777BE8}
AppName={#AppDisplayName}
AppVersion={#AppVersion}
AppVerName={#AppDisplayName} {#AppVersion}
DefaultDirName={localappdata}\Programs\TrainingDeskCalendar
DefaultGroupName={#AppDisplayName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=
UsedUserAreasWarning=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\artifacts\installer
#ifndef OutputBaseFilename
#define OutputBaseFilename "TrainingDeskCalendar-Setup-0.1.0-x64"
#endif
OutputBaseFilename={#OutputBaseFilename}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
CloseApplicationsFilter=TrainingDeskCalendar.App.exe
UninstallDisplayIcon={app}\{#AppExecutable}
UninstallDisplayName={#AppDisplayName}

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Default.isl"

[Messages]
SetupAppTitle=安装程序
SetupWindowTitle=安装 - %1
UninstallAppTitle=卸载程序
UninstallAppFullTitle=卸载 %1
InformationTitle=信息
ConfirmTitle=确认
ErrorTitle=错误
ButtonBack=< 上一步(&B)
ButtonNext=下一步(&N) >
ButtonInstall=安装(&I)
ButtonOK=确定
ButtonCancel=取消
ButtonYes=是(&Y)
ButtonNo=否(&N)
ButtonFinish=完成(&F)
ButtonBrowse=浏览(&B)...
ClickNext=单击“下一步”继续，或单击“取消”退出安装。
WelcomeLabel1=欢迎使用 [name] 安装向导
WelcomeLabel2=本向导将在您的计算机上安装 [name/ver]。%n%n建议在继续前关闭其他应用。
WizardSelectDir=选择安装位置
SelectDirDesc=将 [name] 安装到哪里？
SelectDirLabel3=安装程序将把 [name] 安装到以下文件夹。
SelectDirBrowseLabel=单击“下一步”继续；如需更改位置，请单击“浏览”。
DiskSpaceMBLabel=至少需要 [mb] MB 可用磁盘空间。
WizardSelectTasks=选择附加任务
SelectTasksDesc=需要执行哪些附加任务？
SelectTasksLabel2=请选择安装 [name] 时要执行的附加任务，然后单击“下一步”。
WizardReady=准备安装
ReadyLabel1=安装程序已准备就绪，即将在您的计算机上安装 [name]。
ReadyLabel2a=单击“安装”继续；如需查看或更改设置，请单击“上一步”。
ReadyLabel2b=单击“安装”继续。
ReadyMemoDir=安装位置：
ReadyMemoGroup=开始菜单文件夹：
ReadyMemoTasks=附加任务：
WizardPreparing=正在准备安装
PreparingDesc=安装程序正在准备在您的计算机上安装 [name]。
ApplicationsFound=以下应用正在使用需要更新的文件。建议允许安装程序自动关闭这些应用。
CloseApplications=自动关闭应用(&A)
DontCloseApplications=不关闭应用(&D)
WizardInstalling=正在安装
InstallingLabel=请稍候，安装程序正在您的计算机上安装 [name]。
FinishedHeadingLabel=[name] 安装向导已完成
FinishedLabelNoIcons=[name] 已成功安装。
FinishedLabel=[name] 已成功安装，可通过快捷方式启动。
ClickFinish=单击“完成”退出安装程序。
RunEntryExec=运行 %1
StatusClosingApplications=正在关闭应用...
StatusCreateDirs=正在创建目录...
StatusExtractFiles=正在解压文件...
StatusCreateIcons=正在创建快捷方式...
StatusCreateRegistryEntries=正在写入注册表...
StatusSavingUninstall=正在保存卸载信息...
StatusRunProgram=正在完成安装...
ConfirmUninstall=确定要完全卸载 %1 及其程序文件吗？
UninstallStatusLabel=请稍候，正在从您的计算机上移除 %1。
UninstalledAll=%1 已成功移除。
WizardUninstalling=卸载状态
StatusUninstalling=正在卸载 %1...

[CustomMessages]
LaunchProgram=启动 %1

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "其他选项："

[Files]
Source: "..\artifacts\windows-x64\payload\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppDisplayName}"; Filename: "{app}\TrainingDeskCalendar.App.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppDisplayName}"; Filename: "{app}\TrainingDeskCalendar.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "TrainingDeskCalendar"; ValueData: """{app}\TrainingDeskCalendar.App.exe"""; ValueType: string; Flags: uninsdeletevalue

[Run]
Filename: "{app}\TrainingDeskCalendar.App.exe"; Description: "{cm:LaunchProgram,{#AppDisplayName}}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Name: "{app}"; Type: filesandordirs

[Code]
var
  DeletePersonalData: Boolean;

function ShowUninstallOptions: Boolean;
var
  OptionsForm: TSetupForm;
  ExplanationLabel: TNewStaticText;
  DeletePersonalDataCheckBox: TNewCheckBox;
  ContinueButton: TNewButton;
  CancelButton: TNewButton;
begin
  OptionsForm := CreateCustomForm(ScaleX(430), ScaleY(170), False, False);
  try
    OptionsForm.Caption := '卸载{#AppDisplayName}';
    OptionsForm.Position := poScreenCenter;

    ExplanationLabel := TNewStaticText.Create(OptionsForm);
    ExplanationLabel.Parent := OptionsForm;
    ExplanationLabel.Left := ScaleX(16);
    ExplanationLabel.Top := ScaleY(16);
    ExplanationLabel.Width := OptionsForm.ClientWidth - ScaleX(32);
    ExplanationLabel.Height := ScaleY(42);
    ExplanationLabel.AutoSize := False;
    ExplanationLabel.WordWrap := True;
    ExplanationLabel.Caption := '默认保留训练计划和设置。只有明确勾选下方选项才会删除个人数据。';

    DeletePersonalDataCheckBox := TNewCheckBox.Create(OptionsForm);
    DeletePersonalDataCheckBox.Parent := OptionsForm;
    DeletePersonalDataCheckBox.Left := ScaleX(16);
    DeletePersonalDataCheckBox.Top := ScaleY(70);
    DeletePersonalDataCheckBox.Width := OptionsForm.ClientWidth - ScaleX(32);
    DeletePersonalDataCheckBox.Caption := '同时删除我的所有训练计划和设置（不可恢复）';
    DeletePersonalDataCheckBox.Checked := False;

    ContinueButton := TNewButton.Create(OptionsForm);
    ContinueButton.Parent := OptionsForm;
    ContinueButton.Caption := '继续卸载';
    ContinueButton.ModalResult := mrOk;
    ContinueButton.Default := True;
    ContinueButton.Width := ScaleX(96);
    ContinueButton.Height := ScaleY(25);
    ContinueButton.Left := OptionsForm.ClientWidth - ScaleX(216);
    ContinueButton.Top := OptionsForm.ClientHeight - ScaleY(41);

    CancelButton := TNewButton.Create(OptionsForm);
    CancelButton.Parent := OptionsForm;
    CancelButton.Caption := '取消';
    CancelButton.ModalResult := mrCancel;
    CancelButton.Cancel := True;
    CancelButton.Width := ScaleX(88);
    CancelButton.Height := ScaleY(25);
    CancelButton.Left := OptionsForm.ClientWidth - ScaleX(104);
    CancelButton.Top := ContinueButton.Top;

    Result := OptionsForm.ShowModal = mrOk;
    if Result then
      DeletePersonalData := DeletePersonalDataCheckBox.Checked;
  finally
    OptionsForm.Free;
  end;
end;

function CmdLineParamExists(const Parameter: String): Boolean;
var
  Index: Integer;
begin
  Result := False;
  for Index := 1 to ParamCount do
  begin
    if CompareText(ParamStr(Index), Parameter) = 0 then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

function IsSilentUninstall: Boolean;
begin
  Result := CmdLineParamExists('/SILENT') or
    CmdLineParamExists('/VERYSILENT');
end;

function InitializeUninstall: Boolean;
begin
  DeletePersonalData := False;
  if IsSilentUninstall then
  begin
    DeletePersonalData := CmdLineParamExists('/DELETEUSERDATA');
    Result := True;
  end
  else
    Result := ShowUninstallOptions;
end;

procedure DeletePersonalDataSafely;
var
  DeleteTarget: String;
  ExpectedTarget: String;
begin
  DeleteTarget := RemoveBackslashUnlessRoot(
    ExpandFileName(ExpandConstant('{localappdata}\TrainingDeskCalendar')));
  ExpectedTarget := RemoveBackslashUnlessRoot(
    ExpandFileName(AddBackslash(ExpandConstant('{localappdata}')) + 'TrainingDeskCalendar'));

  if CompareText(DeleteTarget, ExpectedTarget) <> 0 then
    RaiseException('个人数据目录校验失败，已取消删除。');

  if DirExists(DeleteTarget) and
     (not DelTree(DeleteTarget, True, True, True)) then
    RaiseException('无法完全删除个人数据目录。');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and DeletePersonalData then
    DeletePersonalDataSafely;
end;

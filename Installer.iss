[Setup]
AppName=Security Monitor (Comandante / Soldado)
AppVersion=2.1
AppPublisher=Security Monitor
DefaultDirName={pf}\SecurityMonitor
DefaultGroupName=Security Monitor
OutputBaseFilename=SecurityMonitorInstaller
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin

[Types]
Name: "commander"; Description: "Comandante (Servidor que recibe los pulsos)"
Name: "soldier"; Description: "Soldado (Agente que envía los pulsos)"

[Components]
Name: "soldier"; Description: "Agente Soldado (Requerido)"; Types: soldier commander; Flags: fixed
Name: "commander"; Description: "Servidor Comandante Local (Opcional - Uso en LAN)"; Types: commander

[Files]
; Archivos del Comandante (App Desktop)
Source: "Publish\Commander\*"; DestDir: "{app}\Commander"; Components: commander; Flags: ignoreversion recursesubdirs createallsubdirs

; Archivos del Soldado - Servicio (solo el .exe + config)
Source: "Publish\SoldierService\SecurityMonitor.Soldier.exe"; DestDir: "{app}\Soldier"; Components: soldier; Flags: ignoreversion
Source: "Publish\SoldierService\appsettings.json"; DestDir: "{app}\Soldier"; Components: soldier; Flags: ignoreversion

; Archivos del Soldado - Config Tool (bandeja)
Source: "Publish\SoldierConfig\*"; DestDir: "{app}\Soldier"; Components: soldier; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{commondesktop}\Security Monitor Commander"; Filename: "{app}\Commander\SecurityMonitor.Commander.Desktop.exe"; Components: commander; IconFilename: "{app}\Commander\SecurityMonitor.Commander.Desktop.exe"
Name: "{group}\Security Monitor Commander"; Filename: "{app}\Commander\SecurityMonitor.Commander.Desktop.exe"; Components: commander; IconFilename: "{app}\Commander\SecurityMonitor.Commander.Desktop.exe"
Name: "{userstartup}\Security Monitor Commander"; Filename: "{app}\Commander\SecurityMonitor.Commander.Desktop.exe"; Components: commander; IconFilename: "{app}\Commander\SecurityMonitor.Commander.Desktop.exe"
Name: "{group}\Configurar Soldado"; Filename: "{app}\Soldier\SecurityMonitor.Soldier.Config.exe"; Components: soldier; IconFilename: "{app}\Soldier\SecurityMonitor.Soldier.Config.exe"
Name: "{userstartup}\Security Monitor Soldado"; Filename: "{app}\Soldier\SecurityMonitor.Soldier.Config.exe"; Components: soldier; IconFilename: "{app}\Soldier\SecurityMonitor.Soldier.Config.exe"

[Run]
; Firewall Comandante
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""SecurityMon TCP 5000"" dir=in action=allow protocol=TCP localport=5000 profile=any enable=yes"; Flags: runhidden; Components: commander
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""SecurityMon UDP 47777 In"" dir=in action=allow protocol=UDP localport=47777 profile=any enable=yes"; Flags: runhidden; Components: commander
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""SecurityMon UDP 47777 Out"" dir=out action=allow protocol=UDP localport=47777 profile=any enable=yes"; Flags: runhidden; Components: commander

; Firewall Soldado
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""SecurityMon Soldier UDP 47777"" dir=in action=allow protocol=UDP localport=47777 profile=any enable=yes"; Flags: runhidden; Components: soldier

; Instalar Servicio Soldado
Filename: "sc.exe"; Parameters: "create SecurityMonitorSoldier binPath= ""{app}\Soldier\SecurityMonitor.Soldier.exe"" start= delayed-auto displayname= ""Security Monitor Soldier Service"""; Components: soldier; Flags: runhidden
Filename: "sc.exe"; Parameters: "failure SecurityMonitorSoldier reset= 86400 actions= restart/5000/restart/10000/restart/30000"; Components: soldier; Flags: runhidden
Filename: "sc.exe"; Parameters: "start SecurityMonitorSoldier"; Components: soldier; Flags: runhidden

; Lanzar al finalizar instalación
Filename: "{app}\Commander\SecurityMonitor.Commander.Desktop.exe"; Description: "Ejecutar Comandante ahora"; Flags: nowait postinstall skipifsilent; Components: commander
Filename: "{app}\Soldier\SecurityMonitor.Soldier.Config.exe"; Description: "Ejecutar Bandeja del Soldado ahora"; Flags: nowait postinstall skipifsilent; Components: soldier


[UninstallRun]
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""SecurityMon TCP 5000"""; Flags: runhidden
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""SecurityMon UDP 47777 In"""; Flags: runhidden
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""SecurityMon UDP 47777 Out"""; Flags: runhidden
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""SecurityMon Soldier UDP 47777"""; Flags: runhidden
Filename: "sc.exe"; Parameters: "stop SecurityMonitorSoldier"; Flags: runhidden
Filename: "sc.exe"; Parameters: "delete SecurityMonitorSoldier"; Flags: runhidden

[Code]
var
  ConfigPage: TInputQueryWizardPage;

function GetLocalIP: string;
var
  WbemLocator, WbemService, QueryResult, Item: Variant;
begin
  Result := 'localhost';
  try
    WbemLocator := CreateOleObject('WbemScripting.SWbemLocator');
    WbemService := WbemLocator.ConnectServer('', 'root\CIMV2');
    QueryResult := WbemService.ExecQuery('SELECT IPAddress FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True');
    if not VarIsClear(QueryResult) then
    begin
      if QueryResult.Count > 0 then
      begin
        Item := QueryResult.ItemIndex(0);
        if not VarIsClear(Item.IPAddress) then
          Result := Item.IPAddress[0];
      end;
    end;
  except
    // Fallback to localhost if WMI fails
  end;
end;

procedure InitializeWizard;
begin
  ConfigPage := CreateInputQueryPage(wpSelectComponents,
    'Configuración de Red', 'Conectividad del Soldado',
    'Por favor ingrese los datos necesarios para que el Soldado se comunique con el Comandante.');
  ConfigPage.Add('Nombre de la Red Autorizada (SSID):', False);
  ConfigPage.Add('Dirección IP del Comandante (Master):', False);
  ConfigPage.Add('ID de Organización (Account ID) - Opcional:', False);
  
  ConfigPage.Values[0] := 'FUS-ADMIN';
  ConfigPage.Values[1] := 'https://cloud-commander.pythonanywhere.com/api/heartbeat'; 
  ConfigPage.Values[2] := 'MI_EMPRESA_01'; 
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  { Mostrar la página si se instala el soldado O el comandante }
  if (PageID = ConfigPage.ID) and not (IsComponentSelected('soldier') or IsComponentSelected('commander')) then
    Result := True
  else
    Result := False;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = ConfigPage.ID then
  begin
    if IsComponentSelected('commander') and (ConfigPage.Values[1] = '') then
    begin
      ConfigPage.Values[1] := GetLocalIP();
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  AppConfigPath: string;
  AnsiContents: AnsiString;
  UnicodeContents: string;
  SelectedSsid, SelectedIp, FinalUrl: string;
begin
  if CurStep = ssPostInstall then
  begin
    SelectedSsid := ConfigPage.Values[0];
    SelectedIp := ConfigPage.Values[1];

    if IsComponentSelected('commander') then
    begin
      AppConfigPath := ExpandConstant('{app}\Commander\appsettings.json');
      if LoadStringFromFile(AppConfigPath, AnsiContents) then
      begin
        UnicodeContents := string(AnsiContents);
        StringChangeEx(UnicodeContents, 'WIFI_EMPRESA_SSID_AQUI', SelectedSsid, True);
        SaveStringToFile(AppConfigPath, AnsiString(UnicodeContents), False);
      end;
    end;

    if IsComponentSelected('soldier') then
    begin
      if (SelectedIp = '') or (SelectedIp = 'AUTO_DISCOVERY') then
        FinalUrl := 'AUTO_DISCOVERY'
      else if Pos('http', SelectedIp) > 0 then
        FinalUrl := SelectedIp
      else
        FinalUrl := 'http://' + SelectedIp + ':5000/api/heartbeat';
      
      AppConfigPath := ExpandConstant('{app}\Soldier\appsettings.json');
      
      if LoadStringFromFile(AppConfigPath, AnsiContents) then
      begin
        UnicodeContents := string(AnsiContents);
        { Reemplazar SSID }
        StringChangeEx(UnicodeContents, 'WIFI_EMPRESA_SSID_AQUI', SelectedSsid, True);
        { Reemplazar URL por defecto }
        StringChangeEx(UnicodeContents, 'http://localhost:5000/api/heartbeat', FinalUrl, True);
        { Reemplazar o insertar AccountId }
        if Pos('"AccountId":', UnicodeContents) > 0 then
          StringChangeEx(UnicodeContents, '"AccountId": "DEFAULT_USER"', '"AccountId": "' + ConfigPage.Values[2] + '"', True)
        else
          StringChangeEx(UnicodeContents, '"AgentSettings": {', '"AgentSettings": {' + #13#10 + '    "AccountId": "' + ConfigPage.Values[2] + '",', True);
        
        SaveStringToFile(AppConfigPath, AnsiString(UnicodeContents), False);
      end;
    end;
  end;
end;

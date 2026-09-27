@echo off
rem Lanceur du receveur de webhook d'une instance KoXo ISOLEE (DEV).
rem
rem Argument unique : chemin absolu du fichier de definition JSON de
rem l'instance. Le prefixe d'ecoute, le jeton (lu dans le fichier designe par
rem receiver.tokenPath) et les journaux viennent de cette definition : aucun
rem secret ne passe sur la ligne de commande.
rem
rem Le lanceur de production reste Start-KoxoSyncWebhookReceiver-8042.cmd,
rem inchange.
rem
rem -Command "& ..." et non -File : sous Windows PowerShell 5.1, un script
rem lance par -File n'a pas $PSScriptRoot dans les valeurs par defaut de ses
rem parametres, et le receveur echouait des son demarrage (constate sur SRV-21
rem le 2026-09-26). Le lanceur 8042 utilise deja cette forme.
rem
rem Tache planifiee : passer le chemin de definition SANS guillemets (il ne
rem doit donc pas contenir d'espace). Sinon cmd /c retire les guillemets
rem exterieurs et tente d'executer C:\Program.

setlocal

if "%~1"=="" (
    echo Usage: %~nx0 ^<chemin absolu de la definition d'instance^>
    exit /b 2
)

cd /d "%~dp0"

powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "& '%~dp0Start-KoxoSyncWebhookReceiver.ps1' -InstanceConfigPath '%~1'"

endlocal

@echo off
rem Lanceur du receveur de webhook d'une instance KoXo ISOLEE (DEV).
rem
rem Argument unique : chemin absolu du fichier de definition JSON de
rem l'instance. Le prefixe d'ecoute, le jeton (lu dans le fichier designe par
rem receiver.tokenPath) et les journaux viennent de cette definition : aucun
rem secret ne passe sur la ligne de commande.
rem
rem Le lanceur de production reste Start-KoxoSyncWebhookReceiver-8042.cmd,
rem inchange. Aucune tache planifiee n'appelle encore ce fichier.

setlocal

if "%~1"=="" (
    echo Usage: %~nx0 ^<chemin absolu de la definition d'instance^>
    exit /b 2
)

cd /d "%~dp0"

powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%~dp0Start-KoxoSyncWebhookReceiver.ps1" -InstanceConfigPath "%~1"

endlocal

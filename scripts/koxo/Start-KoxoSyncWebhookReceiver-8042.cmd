@echo off
rem Lanceur du receveur de webhook KoXo.
rem
rem Lit le jeton depuis koxo-webhook-token.txt place a cote, puis demarre le
rem receveur. Le port se passe en premier argument, 8042 par defaut.
rem
rem Les chemins sont deduits de %~dp0 : ce fichier fonctionne donc depuis le
rem depot comme depuis C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro.
rem
rem La tache planifiee Kermaria-KoXoWebhookReceiver-8042 n'appelle PAS ce
rem fichier : elle invoque powershell.exe directement. Ce lanceur sert aux
rem demarrages manuels et de reference pour reconstruire la tache.
rem
rem Ne jamais echapper le $ de PowerShell ici : la version deployee sur SRV-21
rem portait `$t au lieu de $t, ce qui transmettait la chaine litterale « $t »
rem comme jeton au lieu de la valeur lue.

setlocal

set "PORT=%~1"
if "%PORT%"=="" set "PORT=8042"

cd /d "%~dp0"

powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "$t=[System.IO.File]::ReadAllText('%~dp0koxo-webhook-token.txt').Trim(); & '%~dp0Start-KoxoSyncWebhookReceiver.ps1' -Prefix 'http://+:%PORT%/internal/koxo/sync/' -Token $t"

endlocal

set SERVICE_PATH=d:\Project\1218_Giraffe_Labs\Sources\Dentrix-Plugin\DentrixPlugin\ChewsiPlugin.Service\bin\Debug\ChewsiPlugin.Service.exe
set INSTALL_UTIL_HOME=c:\Windows\Microsoft.NET\Framework\v4.0.30319
set SERVICE_NAME = "Chewsi Service"

set PATH=%PATH%;%INSTALL_UTIL_HOME%

installutil /SName=%SERVICE_NAME% %SERVICE_PATH%

echo Done

pause
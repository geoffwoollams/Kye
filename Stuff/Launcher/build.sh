#!/bin/sh

rm -rf src/bin src/obj nsis/app.ico src/app.ico bin/Release/net48/Launcher.exe nsis/Kye-Setup.exe

magick app.png -define icon:auto-resize=256,128,64,48,32,16 src/app.ico
cp src/app.ico nsis/app.ico

cd src
dotnet build -c Release

cp bin/Release/net48/Launcher.exe ../nsis/Launcher.exe

cd ../nsis
makensis Kye.nsi

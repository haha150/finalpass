#!/bin/bash
cp -r ../desktop/* packages/se.symeri.finalpass/data/
rm packages/se.symeri.finalpass/data/test.db packages/se.symeri.finalpass/data/config.json
CGO_ENABLED=1 go build -C packages/se.symeri.finalpass/data/ -tags=release -ldflags="-s -w -H=windowsgui" -o .Finalpass.exe
rm -r packages/se.symeri.finalpass/data/*
mv packages/se.symeri.finalpass/data/.Finalpass.exe packages/se.symeri.finalpass/data/Finalpass.exe
cp ../desktop/config.env packages/se.symeri.finalpass/data/
cp -r ../desktop/qtbox packages/se.symeri.finalpass/data/
cp -r ../desktop/icons packages/se.symeri.finalpass/data/
/c/Qt/QtIFW-4.6.1/bin/binarycreator.exe -f -c config/config.xml -p packages/ -t /c/Qt/QtIFW-4.6.1/bin/installerbase.exe finalpass-installer.exe
mkdir dist
mv finalpass-installer.exe dist/
rm -r packages/se.symeri.finalpass/data/*
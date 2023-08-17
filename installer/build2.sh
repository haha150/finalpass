#!/bin/bash
cp -r ../desktop/* packages/se.symeri.finalpass/data/
rm packages/se.symeri.finalpass/data/test.db packages/se.symeri.finalpass/data/config.json
CGO_ENABLED=1 go build -C packages/se.symeri.finalpass/data/ -tags=release -ldflags="-s -w" -o .finalpass.exe
rm -r packages/se.symeri.finalpass/data/*
mv packages/se.symeri.finalpass/data/.finalpass.exe packages/se.symeri.finalpass/data/finalpass.exe
cp ../desktop/config.env packages/se.symeri.finalpass/data/
cp -r ../desktop/qtbox packages/se.symeri.finalpass/data/
cp -r ../desktop/icons packages/se.symeri.finalpass/data/
/c/Qt/QtIFW-4.6.0/bin/binarycreator.exe -f -c config/config.xml -p packages/ -t /c/Qt/QtIFW-4.6.0/bin/installerbase.exe finalpass-installer.exe
mv finalpass-installer.exe dist/
rm -r packages/se.symeri.finalpass/data/*
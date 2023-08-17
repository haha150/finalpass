#!/bin/bash
cp -r ../desktop/* packages/se.symeri.finalpass/data/
rm packages/se.symeri.finalpass/data/test.db packages/se.symeri.finalpass/data/config.json
go build -C packages/se.symeri.finalpass/data/ -tags=release -ldflags="-s -w" -o .finalpass
rm -r packages/se.symeri.finalpass/data/*
mv packages/se.symeri.finalpass/data/.finalpass packages/se.symeri.finalpass/data/finalpass
cp ../desktop/config.env packages/se.symeri.finalpass/data/
cp ../desktop/qtbox packages/se.symeri.finalpass/data/
cp -r ../desktop/icons packages/se.symeri.finalpass/data/
/home/ubuntu/Qt/QtIFW-4.6.0/bin/binarycreator -f -c config/config.xml -p packages/ -t /home/ubuntu/Qt/QtIFW-4.6.0/bin/installerbase finalpass-installer
mkdir dist
mv finalpass-installer dist/
rm -r packages/se.symeri.finalpass/data/*
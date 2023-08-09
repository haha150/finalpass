#!/bin/bash
cp -r ../desktop/* packages/se.symeri.finalpass/data/
cp ../desktop/.env packages/se.symeri.finalpass/data/
rm packages/se.symeri.finalpass/data/test.db packages/se.symeri.finalpass/data/config.json
/home/ubuntu/Qt/QtIFW-4.6.0/bin/binarycreator --offline-only -c config/config.xml -p packages/ -t /home/ubuntu/Qt/QtIFW-4.6.0/bin/installerbase finalpass-installer
mv finalpass-installer dist/
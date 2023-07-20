# password-manager

## Install Go

sudo apt -y install build-essential libglu1-mesa-dev libpulse-dev libglib2.0-dev libqt*5-dev qt*5-dev

wget https://go.dev/dl/go1.20.6.linux-amd64.tar.gz

tar -xvf go1.20.6.linux-amd64.tar.gz

sudo mv go /usr/local/

export PATH=$PATH:/usr/local/go/bin

## Build & run

go mod init password-manager

go get github.com/therecipe/qt/core

go get github.com/therecipe/qt/widgets

go get golang.org/x/crypto

go run .
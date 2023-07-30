# finalpass

## Install Go

sudo apt -y install build-essential libglu1-mesa-dev libpulse-dev libglib2.0-dev libqt*5-dev qt*5-dev

wget https://go.dev/dl/go1.20.6.linux-amd64.tar.gz

tar -xvf go1.20.6.linux-amd64.tar.gz

sudo mv go /usr/local/

export PATH=$PATH:/usr/local/go/bin

## Build & run desktop app

go mod init finalpass

go get github.com/therecipe/qt/core

go get github.com/therecipe/qt/widgets

go get golang.org/x/crypto

go get gorm.io/gorm

go get gorm.io/driver/sqlite

go run .

### Linux

go build -tags=release -ldflags="-s -w" -o finalpass

### Windows

install choco

Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))

choco install mingw

CGO_ENABLED=1 go build -tags=release -ldflags="-s -w" -o finalpass.exe

## Build & run api

go mid init api

go get github.com/gin-gonic/gin

go get github.com/dgrijalva/jwt-go

go get github.com/pquerna/otp

go get gorm.io/gorm

go get github.com/gin-contrib/cors

go get gorm.io/driver/sqlite

go get github.com/google/uuid

go run .

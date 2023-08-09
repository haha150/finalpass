package models

import (
	"gorm.io/gorm"
)

var Url string = ""
var Password string = ""

type Database struct {
	gorm.Model
	ID           int
	Name         string        `gorm:"unique;not null"`
	SecretGroups []SecretGroup `gorm:"foreignkey:DatabaseID"`
}

type SecretGroup struct {
	gorm.Model
	ID         int
	Name       string `gorm:"not null"`
	DatabaseID int
	Secrets    []Secret `gorm:"foreignkey:SecretGroupID"`
}

type Secret struct {
	gorm.Model
	ID            int
	Username      string `gorm:"not null"`
	Password      []byte `gorm:"not null"`
	Title         string
	Description   string
	URL           string
	SecretGroupID int
}

type Configuration struct {
	Database string
}

type User struct {
	Email    string
	Token    string
	Verified bool
	Totp     bool
}

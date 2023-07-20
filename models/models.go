package models

import (
	"gorm.io/gorm"
)

type Database struct {
	gorm.Model
	ID         int
	Name       string     `gorm:"unique;not null"`
	Categories []Category `gorm:"foreignkey:DatabaseID"`
}

type Category struct {
	gorm.Model
	ID         int
	Name       string `gorm:"not null"`
	DatabaseID int
	Secrets    []Secret `gorm:"foreignkey:CategoryID"`
}

type Secret struct {
	gorm.Model
	ID          int
	Username    string `gorm:"not null"`
	Password    string `gorm:"not null"`
	Title       string
	Description string
	URL         string
	CategoryID  int
}

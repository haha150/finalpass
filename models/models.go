package models

import (
	"gorm.io/gorm"
)

type Database struct {
	gorm.Model
	ID   int
	Name string `gorm:"not null"`
}

package controller

import (
	"fmt"
	"password-manager/models"

	"gorm.io/driver/sqlite"
	"gorm.io/gorm"
)

func Init() error {
	db, err := gorm.Open(sqlite.Open("database.db"), &gorm.Config{})
	if err != nil {
		fmt.Println(err)
		return err
	}

	// Create the database if it doesn't exist
	err = db.AutoMigrate(&models.Database{})
	if err != nil {
		fmt.Println(err)
		return err
	}

	return nil
}

// create new row in table database
func CreateDatabase(name string) error {
	db, err := gorm.Open(sqlite.Open("database.db"), &gorm.Config{})
	if err != nil {
		fmt.Println(err)
		return err
	}

	result := db.Create(&models.Database{Name: name})
	if result.Error != nil {
		return result.Error
	}

	return nil
}

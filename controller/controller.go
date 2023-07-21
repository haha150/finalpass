package controller

import (
	"fmt"
	"log"
	"os"
	"password-manager/models"

	"gorm.io/driver/sqlite"
	"gorm.io/gorm"
)

func CheckFileExist(file string) bool {
	log.Println("Check if file exist")
	if _, err := os.Stat(file); err == nil {
		return true
	}
	return false
}

func InitDB(file string) error {
	log.Println("Init database")
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return err
	}
	err = db.AutoMigrate(&models.Database{}, &models.SecretGroup{}, &models.Secret{})
	if err != nil {
		log.Println(err)
		return err
	}
	return nil
}

func CreateDatabaseAndSecretGroupIfNotExist(file string, name string) error {
	log.Println("Create database and secret group if not exist")
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return err
	}
	log.Printf("Database file created: %s", file)
	var database models.Database
	err2 := db.First(&database).Error
	if err2 != nil && err2 == gorm.ErrRecordNotFound {
		database.Name = name
		db.Create(&database)
	}
	log.Printf("Created database: %s", database.Name)
	var group models.SecretGroup
	err3 := db.First(&group, "name = ?", "General").Error
	if err3 != nil && err3 == gorm.ErrRecordNotFound {
		group.Name = "General"
		group.DatabaseID = database.ID
		db.Create(&group)
	}
	log.Printf("Created secret group: %s", group.Name)
	return nil
}

func GetAllDatabases(file string) ([]models.Database, error) {
	log.Println("Get all databases with secret groups and secrets")
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return nil, err
	}
	var databases []models.Database
	result := db.Preload("SecretGroups.Secrets").Find(&databases)
	if result.Error != nil {
		return nil, result.Error
	}
	return databases, nil
}

func CreateSecretGroup(file string, d string, name string) (models.SecretGroup, error) {
	log.Println("Create secret group")
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return models.SecretGroup{}, err
	}
	var database models.Database
	db.Preload("SecretGroups").First(&database, "name = ?", d)
	for _, group := range database.SecretGroups {
		if group.Name == name {
			return models.SecretGroup{}, fmt.Errorf("secret group already exists")
		}
	}
	var group models.SecretGroup
	group.Name = name
	group.DatabaseID = database.ID
	db.Create(&group)
	return group, nil
}

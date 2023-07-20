package controller

import (
	"fmt"
	"log"
	"password-manager/models"

	"gorm.io/driver/sqlite"
	"gorm.io/gorm"
)

func Init() error {
	log.Println("Init database")
	db, err := gorm.Open(sqlite.Open("database.db"), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return err
	}
	err = db.AutoMigrate(&models.Database{}, &models.Category{}, &models.Secret{})
	if err != nil {
		fmt.Println(err)
		return err
	}
	return nil
}

func CreateDatabase(name string) (models.Database, error) {
	log.Println("Create database")
	db, err := gorm.Open(sqlite.Open("database.db"), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return models.Database{}, err
	}
	var database models.Database
	database.Name = name
	db.Create(&database)
	var category models.Category
	category.Name = "General"
	category.DatabaseID = database.ID
	db.Create(&category)
	return database, nil
}

func CreateDatabaseAndCategoryIfNotExist() error {
	log.Println("Create database and category if not exist")
	db, err := gorm.Open(sqlite.Open("database.db"), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return err
	}
	var database models.Database
	err2 := db.First(&database).Error
	if err2 != nil && err2 == gorm.ErrRecordNotFound {
		database.Name = "Database"
		db.Create(&database)
	}
	var category models.Category
	err3 := db.First(&category, "name = ?", "General").Error
	if err3 != nil && err3 == gorm.ErrRecordNotFound {
		category.Name = "General"
		category.DatabaseID = database.ID
		db.Create(&category)
	}
	return nil
}

func GetAllDatabases() ([]models.Database, error) {
	log.Println("Get all databases with categories and secrets")
	db, err := gorm.Open(sqlite.Open("database.db"), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return nil, err
	}
	var databases []models.Database
	result := db.Preload("Categories.Secrets").Find(&databases)
	if result.Error != nil {
		return nil, result.Error
	}
	return databases, nil
}

func OpenDatabase() {
	log.Println("Open database")
}

func CreateCategory(d string, name string) (models.Category, error) {
	log.Println("Create category")
	db, err := gorm.Open(sqlite.Open("database.db"), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return models.Category{}, err
	}
	var database models.Database
	db.Preload("Categories").First(&database, "name = ?", d)
	for _, category := range database.Categories {
		if category.Name == name {
			return models.Category{}, fmt.Errorf("category already exists")
		}
	}
	var category models.Category
	category.Name = name
	category.DatabaseID = database.ID
	db.Create(&category)
	return category, nil
}

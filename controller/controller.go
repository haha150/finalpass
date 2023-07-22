package controller

import (
	"fmt"
	"log"
	"math/rand"
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

func GetSecrets(file string, d string, g string) ([]models.Secret, error) {
	log.Println("Get secrets")
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return nil, err
	}
	var database models.Database
	db.Preload("SecretGroups.Secrets").First(&database, "name = ?", d)
	for _, group := range database.SecretGroups {
		if group.Name == g {
			return group.Secrets, nil
		}
	}
	return nil, fmt.Errorf("secret group not found")
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

func CreateSecret(file string, d string, g string, s models.Secret) (models.Secret, error) {
	log.Println("Create secret")
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return models.Secret{}, err
	}
	var database models.Database
	db.Preload("SecretGroups").First(&database, "name = ?", d)
	for _, grp := range database.SecretGroups {
		if grp.Name == g {
			s.SecretGroupID = grp.ID
			db.Create(&s)
			return s, nil
		}
	}
	return models.Secret{}, fmt.Errorf("secret group not found")
}

func GenerateStrongPassword(length int) string {
	characters := "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-_=+"
	lowerCase := []rune(characters[:26])
	upperCase := []rune(characters[26:52])
	digits := []rune(characters[52:62])
	specialCharacters := []rune(characters[62:])

	password := ""
	for i := 0; i < length; i++ {
		charType := rand.Intn(4)
		switch charType {
		case 0:
			password += string(lowerCase[rand.Intn(len(lowerCase))])
		case 1:
			password += string(upperCase[rand.Intn(len(upperCase))])
		case 2:
			password += string(digits[rand.Intn(len(digits))])
		case 3:
			password += string(specialCharacters[rand.Intn(len(specialCharacters))])
		}
	}
	return password
}

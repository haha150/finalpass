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

func CreateSubDatabase(file string, name string) (models.Database, error) {
	log.Println("Create sub database")
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return models.Database{}, err
	}
	var database models.Database
	err2 := db.First(&database).Error
	if err2 != nil && err2 == gorm.ErrRecordNotFound {
		return models.Database{}, fmt.Errorf("database not found")
	}
	var subDatabase models.Database
	err3 := db.First(&subDatabase, "name = ?", name).Error
	if err3 != nil && err3 == gorm.ErrRecordNotFound {
		subDatabase.Name = name
		db.Create(&subDatabase)
		var group models.SecretGroup
		group.Name = "General"
		group.DatabaseID = subDatabase.ID
		db.Create(&group)
		return subDatabase, nil
	}
	return models.Database{}, fmt.Errorf("database not found")
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

func GetDatabase(file string, d string) (models.Database, error) {
	log.Println("Get database")
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return models.Database{}, err
	}
	var database models.Database
	result := db.Preload("SecretGroups").First(&database, "name = ?", d)
	if result.Error != nil {
		return models.Database{}, result.Error
	}
	return database, nil
}

func UpdateDatabase(file string, d string, name string) (models.Database, error) {
	log.Println("Update database")
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return models.Database{}, err
	}
	var database models.Database
	result := db.Find(&database, "name = ?", d)
	if result.Error != nil {
		return models.Database{}, result.Error
	}
	database.Name = name
	db.Save(&database)
	return database, nil
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

func GetSecret(file string, d string, g string, s int) (models.Secret, error) {
	log.Println("Get secret")
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return models.Secret{}, err
	}
	var database models.Database
	db.Preload("SecretGroups.Secrets").First(&database, "name = ?", d)
	for _, group := range database.SecretGroups {
		if group.Name == g {
			for _, secret := range group.Secrets {
				if secret.ID == s {
					return secret, nil
				}
			}
		}
	}
	return models.Secret{}, fmt.Errorf("secret not found")
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

func GetSecretGroup(file string, d string, g string) (models.SecretGroup, error) {
	log.Println("Get secret group")
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return models.SecretGroup{}, err
	}
	var database models.Database
	db.Preload("SecretGroups").First(&database, "name = ?", d)
	for _, group := range database.SecretGroups {
		if group.Name == g {
			return group, nil
		}
	}
	return models.SecretGroup{}, fmt.Errorf("secret group not found")
}

func UpdateSecretGroup(file string, d string, g string, name string) (models.SecretGroup, error) {
	log.Println("Update secret group")
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
	for _, group := range database.SecretGroups {
		if group.Name == g {
			group.Name = name
			db.Save(&group)
			return group, nil
		}
	}
	return models.SecretGroup{}, fmt.Errorf("secret group not found")
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

func UpdateSecret(file string, d string, g string, id int, s models.Secret) (models.Secret, error) {
	log.Println("Update secret")
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return models.Secret{}, err
	}
	var database models.Database
	db.Preload("SecretGroups.Secrets").First(&database, "name = ?", d)
	for _, group := range database.SecretGroups {
		if group.Name == g {
			for _, secret := range group.Secrets {
				if secret.ID == id {
					secret.Title = s.Title
					secret.Username = s.Username
					secret.Password = s.Password
					secret.URL = s.URL
					secret.Description = s.Description
					db.Save(&secret)
					return secret, nil
				}
			}
		}
	}
	return models.Secret{}, fmt.Errorf("secret group not found")
}

func DeleteSecret(file string, d string, g string, id int) error {
	log.Println("Delete secret")
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return err
	}
	var database models.Database
	db.Preload("SecretGroups.Secrets").First(&database, "name = ?", d)
	for _, group := range database.SecretGroups {
		if group.Name == g {
			for _, secret := range group.Secrets {
				if secret.ID == id {
					db.Delete(&secret)
					return nil
				}
			}
		}
	}
	return fmt.Errorf("secret group not found")
}

func DeleteSecretGroup(file string, d string, g string) error {
	log.Println("Delete secret group")
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return err
	}
	var database models.Database
	db.Preload("SecretGroups.Secrets").First(&database, "name = ?", d)
	for _, group := range database.SecretGroups {
		if group.Name == g {
			for _, secret := range group.Secrets {
				db.Delete(&secret)
			}
			db.Delete(&group)
			return nil
		}
	}
	return fmt.Errorf("secret group not found")
}

func DeleteDatabase(file string, d string) error {
	log.Println("Delete database")
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return err
	}
	var database models.Database
	db.Preload("SecretGroups.Secrets").First(&database, "name = ?", d)
	for _, group := range database.SecretGroups {
		for _, secret := range group.Secrets {
			db.Delete(&secret)
		}
		db.Delete(&group)
	}
	db.Delete(&database)
	return nil
}

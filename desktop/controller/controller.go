package controller

import (
	"bytes"
	"desktop/models"
	"desktop/security"
	"fmt"
	"log"
	"net/http"
	"os"

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

func cleanup(db *gorm.DB) {
	log.Println("Close database")
	dbInstance, _ := db.DB()
	_ = dbInstance.Close()
}

func InitDB(file string, password string) error {
	err := initDB(file, password)
	encrypted := security.EncryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !encrypted {
		return fmt.Errorf("error when encrypting file")
	}
	return err
}

func initDB(file string, password string) error {
	log.Println("Init database")
	db, err := gorm.Open(sqlite.Open(fmt.Sprintf("%s.tmp", file)), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return err
	}
	defer cleanup(db)
	err = db.AutoMigrate(&models.Database{}, &models.SecretGroup{}, &models.Secret{})
	if err != nil {
		log.Println(err)
		return err
	}
	return nil
}

func CreateDatabaseAndSecretGroupIfNotExist(file string, password string, name string) error {
	decrypted := security.DecryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !decrypted {
		return fmt.Errorf("wrong password")
	}
	err := createDatabaseAndSecretGroupIfNotExist(file, name)
	encrypted := security.EncryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !encrypted {
		return fmt.Errorf("error when encrypting file")
	}
	return err
}

func createDatabaseAndSecretGroupIfNotExist(file string, name string) error {
	log.Println("Create database and secret group if not exist")
	db, err := gorm.Open(sqlite.Open(fmt.Sprintf("%s.tmp", file)), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return err
	}
	defer cleanup(db)
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

func CreateSubDatabase(file string, password string, name string) (models.Database, error) {
	decrypted := security.DecryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !decrypted {
		return models.Database{}, fmt.Errorf("wrong password")
	}
	sub, err := createSubDatabase(file, name)
	encrypted := security.EncryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !encrypted {
		return models.Database{}, fmt.Errorf("error when encrypting file")
	}
	return sub, err
}

func createSubDatabase(file string, name string) (models.Database, error) {
	log.Println("Create sub database")
	db, err := gorm.Open(sqlite.Open(fmt.Sprintf("%s.tmp", file)), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return models.Database{}, err
	}
	defer cleanup(db)
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

func GetAllDatabases(file string, password string) ([]models.Database, error) {
	decrypted := security.DecryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !decrypted {
		return nil, fmt.Errorf("wrong password")
	}
	db, err := getAllDatabases(file)
	encrypted := security.EncryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !encrypted {
		return nil, fmt.Errorf("error when encrypting file")
	}
	return db, err
}

func getAllDatabases(file string) ([]models.Database, error) {
	log.Println("Get all databases with secret groups and secrets")
	db, err := gorm.Open(sqlite.Open(fmt.Sprintf("%s.tmp", file)), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return nil, err
	}
	defer cleanup(db)
	var databases []models.Database
	result := db.Preload("SecretGroups.Secrets").Find(&databases)
	if result.Error != nil {
		return nil, result.Error
	}
	return databases, nil
}

func GetDatabase(file string, password string, d string) (models.Database, error) {
	decrypted := security.DecryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !decrypted {
		return models.Database{}, fmt.Errorf("wrong password")
	}
	db, err := getDatabase(file, d)
	encrypted := security.EncryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !encrypted {
		return models.Database{}, fmt.Errorf("error when encrypting file")
	}
	return db, err
}

func getDatabase(file string, d string) (models.Database, error) {
	log.Println("Get database")
	db, err := gorm.Open(sqlite.Open(fmt.Sprintf("%s.tmp", file)), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return models.Database{}, err
	}
	defer cleanup(db)
	var database models.Database
	result := db.Preload("SecretGroups").First(&database, "name = ?", d)
	if result.Error != nil {
		return models.Database{}, result.Error
	}
	return database, nil
}

func UpdateDatabase(file string, password string, d string, name string) (models.Database, error) {
	decrypted := security.DecryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !decrypted {
		return models.Database{}, fmt.Errorf("wrong password")
	}
	db, err := updateDatabase(file, d, name)
	encrypted := security.EncryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !encrypted {
		return models.Database{}, fmt.Errorf("error when encrypting file")
	}
	return db, err
}

func updateDatabase(file string, d string, name string) (models.Database, error) {
	log.Println("Update database")
	db, err := gorm.Open(sqlite.Open(fmt.Sprintf("%s.tmp", file)), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return models.Database{}, err
	}
	defer cleanup(db)
	var database models.Database
	result := db.Find(&database, "name = ?", d)
	if result.Error != nil {
		return models.Database{}, result.Error
	}
	database.Name = name
	db.Save(&database)
	return database, nil
}

func GetSecrets(file string, password string, d string, g string) ([]models.Secret, error) {
	decrypted := security.DecryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !decrypted {
		return nil, fmt.Errorf("wrong password")
	}
	sct, err := getSecrets(file, d, g)
	encrypted := security.EncryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !encrypted {
		return nil, fmt.Errorf("error when encrypting file")
	}
	return sct, err
}

func getSecrets(file string, d string, g string) ([]models.Secret, error) {
	log.Println("Get secrets")
	db, err := gorm.Open(sqlite.Open(fmt.Sprintf("%s.tmp", file)), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return nil, err
	}
	defer cleanup(db)
	var database models.Database
	db.Preload("SecretGroups.Secrets").First(&database, "name = ?", d)
	for _, group := range database.SecretGroups {
		if group.Name == g {
			return group.Secrets, nil
		}
	}
	return nil, fmt.Errorf("secret group not found")
}

func GetSecret(file string, password string, d string, g string, s int) (models.Secret, error) {
	decrypted := security.DecryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !decrypted {
		return models.Secret{}, fmt.Errorf("wrong password")
	}
	sct, err := getSecret(file, d, g, s)
	encrypted := security.EncryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !encrypted {
		return models.Secret{}, fmt.Errorf("error when encrypting file")
	}
	plaintext, err2 := security.DecryptText(password, sct.Password)
	if err2 != nil {
		return models.Secret{}, err2
	}
	sct.Password = plaintext
	return sct, err
}

func getSecret(file string, d string, g string, s int) (models.Secret, error) {
	log.Println("Get secret")
	db, err := gorm.Open(sqlite.Open(fmt.Sprintf("%s.tmp", file)), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return models.Secret{}, err
	}
	defer cleanup(db)
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

func CreateSecretGroup(file string, password string, d string, name string) (models.SecretGroup, error) {
	decrypted := security.DecryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !decrypted {
		return models.SecretGroup{}, fmt.Errorf("wrong password")
	}
	sg, err := createSecretGroup(file, d, name)
	encrypted := security.EncryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !encrypted {
		return models.SecretGroup{}, fmt.Errorf("error when encrypting file")
	}
	return sg, err
}

func createSecretGroup(file string, d string, name string) (models.SecretGroup, error) {
	log.Println("Create secret group")
	db, err := gorm.Open(sqlite.Open(fmt.Sprintf("%s.tmp", file)), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return models.SecretGroup{}, err
	}
	defer cleanup(db)
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

func GetSecretGroup(file string, password string, d string, g string) (models.SecretGroup, error) {
	decrypted := security.DecryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !decrypted {
		return models.SecretGroup{}, fmt.Errorf("wrong password")
	}
	sg, err := getSecretGroup(file, d, g)
	encrypted := security.EncryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !encrypted {
		return models.SecretGroup{}, fmt.Errorf("error when encrypting file")
	}
	return sg, err
}

func getSecretGroup(file string, d string, g string) (models.SecretGroup, error) {
	log.Println("Get secret group")
	db, err := gorm.Open(sqlite.Open(fmt.Sprintf("%s.tmp", file)), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return models.SecretGroup{}, err
	}
	defer cleanup(db)
	var database models.Database
	db.Preload("SecretGroups").First(&database, "name = ?", d)
	for _, group := range database.SecretGroups {
		if group.Name == g {
			return group, nil
		}
	}
	return models.SecretGroup{}, fmt.Errorf("secret group not found")
}

func UpdateSecretGroup(file string, password string, d string, g string, name string) (models.SecretGroup, error) {
	decrypted := security.DecryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !decrypted {
		return models.SecretGroup{}, fmt.Errorf("wrong password")
	}
	sg, err := updateSecretGroup(file, d, g, name)
	encrypted := security.EncryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !encrypted {
		return models.SecretGroup{}, fmt.Errorf("error when encrypting file")
	}
	return sg, err
}

func updateSecretGroup(file string, d string, g string, name string) (models.SecretGroup, error) {
	log.Println("Update secret group")
	db, err := gorm.Open(sqlite.Open(fmt.Sprintf("%s.tmp", file)), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return models.SecretGroup{}, err
	}
	defer cleanup(db)
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

func CreateSecret(file string, password string, d string, g string, s models.Secret) (models.Secret, error) {
	ciphertext, err := security.EncryptText(password, string(s.Password))
	if err != nil || ciphertext == nil {
		return models.Secret{}, err
	}
	s.Password = ciphertext
	decrypted := security.DecryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !decrypted {
		return models.Secret{}, fmt.Errorf("wrong password")
	}
	sct, err := createSecret(file, d, g, s)
	encrypted := security.EncryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !encrypted {
		return models.Secret{}, fmt.Errorf("error when encrypting file")
	}
	return sct, err
}

func createSecret(file string, d string, g string, s models.Secret) (models.Secret, error) {
	log.Println("Create secret")
	db, err := gorm.Open(sqlite.Open(fmt.Sprintf("%s.tmp", file)), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return models.Secret{}, err
	}
	defer cleanup(db)
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

func UpdateSecret(file string, password string, d string, g string, id int, s models.Secret) (models.Secret, error) {
	ciphertext, err := security.EncryptText(password, string(s.Password))
	if err != nil || ciphertext == nil {
		return models.Secret{}, err
	}
	s.Password = ciphertext
	decrypted := security.DecryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !decrypted {
		return models.Secret{}, fmt.Errorf("wrong password")
	}
	sct, err := updateSecret(file, d, g, id, s)
	encrypted := security.EncryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !encrypted {
		return models.Secret{}, fmt.Errorf("error when encrypting file")
	}
	return sct, err
}

func updateSecret(file string, d string, g string, id int, s models.Secret) (models.Secret, error) {
	log.Println("Update secret")
	db, err := gorm.Open(sqlite.Open(fmt.Sprintf("%s.tmp", file)), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return models.Secret{}, err
	}
	defer cleanup(db)
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

func DeleteSecret(file string, password string, d string, g string, id int) error {
	decrypted := security.DecryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !decrypted {
		return fmt.Errorf("wrong password")
	}
	err := deleteSecret(file, d, g, id)
	encrypted := security.EncryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !encrypted {
		return fmt.Errorf("error when encrypting file")
	}
	return err
}

func deleteSecret(file string, d string, g string, id int) error {
	log.Println("Delete secret")
	db, err := gorm.Open(sqlite.Open(fmt.Sprintf("%s.tmp", file)), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return err
	}
	defer cleanup(db)
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

func DeleteSecretGroup(file string, password string, d string, g string) error {
	decrypted := security.DecryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !decrypted {
		return fmt.Errorf("wrong password")
	}
	err := deleteSecretGroup(file, d, g)
	encrypted := security.EncryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !encrypted {
		return fmt.Errorf("error when encrypting file")
	}
	return err
}

func deleteSecretGroup(file string, d string, g string) error {
	log.Println("Delete secret group")
	db, err := gorm.Open(sqlite.Open(fmt.Sprintf("%s.tmp", file)), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return err
	}
	defer cleanup(db)
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

func DeleteDatabase(file string, password string, d string) error {
	decrypted := security.DecryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !decrypted {
		return fmt.Errorf("wrong password")
	}
	err := deleteDatabase(file, d)
	encrypted := security.EncryptFile(file, password, fmt.Sprintf("%s.tmp", file))
	if !encrypted {
		return fmt.Errorf("error when encrypting file")
	}
	return err
}

func deleteDatabase(file string, d string) error {
	log.Println("Delete database")
	db, err := gorm.Open(sqlite.Open(fmt.Sprintf("%s.tmp", file)), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return err
	}
	defer cleanup(db)
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

func SendRequest(url string, reqType string, body []byte, token string) (*http.Response, error) {
	req, err := http.NewRequest(reqType, url, bytes.NewBuffer(body))
	if err != nil {
		log.Println(err)
		return nil, err
	}
	req.Header.Set("Content-Type", "application/json")
	if token != "" {
		req.Header.Set("Authorization", token)
	}
	client := &http.Client{}
	return client.Do(req)
}

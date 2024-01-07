package main

import (
	"bytes"
	"crypto/rand"
	"crypto/sha256"
	"encoding/base64"
	"encoding/hex"
	"fmt"
	"log"
	"net/http"
	"net/smtp"
	"os"
	"regexp"
	"strings"
	"time"

	"github.com/dgrijalva/jwt-go"
	"github.com/gin-contrib/cors"
	"github.com/gin-gonic/gin"
	"github.com/google/uuid"
	"github.com/joho/godotenv"
	"github.com/pquerna/otp"
	"github.com/pquerna/otp/totp"
	"golang.org/x/crypto/bcrypt"
	"gorm.io/driver/sqlite"
	"gorm.io/gorm"
)

var file string = "db/auth.db"
var secretKey []byte
var passwordDesktop string = ""
var passwordIOS string = ""
var email string = ""
var emailPassword string = ""
var url string = ""

type User struct {
	gorm.Model
	ID       int
	Username string `json:"username" gorm:"unique"`
	Password string `json:"password" gorm:"not null"`
	Verified bool   `json:"verified" gorm:"default:false"`
	Code     string `json:"code"`
	Totp     string `json:"totp"`
}

type CustomClaims struct {
	Username string `json:"username"`
	jwt.StandardClaims
}

func initDB() error {
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return err
	}
	err = db.AutoMigrate(&User{})
	if err != nil {
		log.Println(err)
		return err
	}

	return nil
}

func addUser(user User) error {
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return err
	}

	result := db.Create(&user)
	if result.Error != nil {
		log.Println(result.Error)
		return result.Error
	}

	return nil
}

func updateUser(user User) error {
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return err
	}

	result := db.Save(&user)
	if result.Error != nil {
		log.Println(result.Error)
		return result.Error
	}

	return nil
}

func getUser(username string) (User, error) {
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return User{}, err
	}

	var user User
	result := db.Where("username = ?", username).First(&user)
	if result.Error != nil {
		log.Println(result.Error)
		return User{}, result.Error
	}

	return user, nil
}

func getUserByCode(code string) (User, error) {
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return User{}, err
	}

	var user User
	result := db.Where("code = ?", code).First(&user)
	if result.Error != nil {
		log.Println(result.Error)
		return User{}, result.Error
	}

	return user, nil
}

func setVerified(username string) error {
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return err
	}

	result := db.Model(&User{}).Where("username = ?", username).Update("verified", true)
	if result.Error != nil {
		log.Println(result.Error)
		return result.Error
	}

	return nil
}

func setTotp(username string, totp string) error {
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return err
	}

	result := db.Model(&User{}).Where("username = ?", username).Update("totp", totp)
	if result.Error != nil {
		log.Println(result.Error)
		return result.Error
	}

	return nil
}

func removeUser(username string) error {
	db, err := gorm.Open(sqlite.Open(file), &gorm.Config{})
	if err != nil {
		log.Println(err)
		return err
	}

	result := db.Where("username = ?", username).Delete(&User{})
	if result.Error != nil {
		log.Println(result.Error)
		return result.Error
	}

	return nil
}

func generateCode() string {
	code := uuid.New()
	return code.String()
}

func generateRandomKey(keyLength int) ([]byte, error) {
	key := make([]byte, keyLength)
	_, err := rand.Read(key)
	if err != nil {
		return nil, err
	}
	return key, nil
}

func generateToken(username string) (string, error) {
	claims := CustomClaims{
		Username: username,
		StandardClaims: jwt.StandardClaims{
			ExpiresAt: time.Now().Add(time.Hour * 2).Unix(),
		},
	}
	token := jwt.NewWithClaims(jwt.SigningMethodHS256, claims)
	return token.SignedString(secretKey)
}

func CheckPasswordHash(password, hash string) bool {
	err := bcrypt.CompareHashAndPassword([]byte(hash), []byte(password))
	return err == nil
}

func authMiddleware() gin.HandlerFunc {
	return func(c *gin.Context) {
		tokenString := c.GetHeader("Authorization")
		if tokenString == "" {
			c.JSON(http.StatusUnauthorized, gin.H{"message": "Unauthorized"})
			c.Abort()
			return
		}

		token, err := jwt.ParseWithClaims(tokenString, &CustomClaims{}, func(token *jwt.Token) (interface{}, error) {
			return secretKey, nil
		})

		if err != nil {
			c.JSON(http.StatusUnauthorized, gin.H{"message": "Unauthorized"})
			c.Abort()
			return
		}

		if claims, ok := token.Claims.(*CustomClaims); ok && token.Valid {
			c.Set("username", claims.Username)
			c.Next()
		} else {
			c.JSON(http.StatusUnauthorized, gin.H{"message": "Unauthorized"})
			c.Abort()
		}
	}
}

func loginHandler(c *gin.Context) {
	if c.Request.Method == "GET" {
		challenge, err := generateRandomKey(32)
		if err != nil {
			c.JSON(http.StatusBadRequest, gin.H{"message": "Invalid request body"})
			return
		}
		c.JSON(http.StatusOK, gin.H{"challenge": hex.EncodeToString(challenge)})
		return
	}

	chall := c.GetHeader("X-Auth-Challenge")
	hash := c.GetHeader("X-Auth-Hash")
	if hash == "" {
		c.JSON(http.StatusBadRequest, gin.H{"message": "Invalid request body"})
		return
	}

	expectedHash := sha256.Sum256([]byte(chall + passwordDesktop))
	expectedHashString := hex.EncodeToString(expectedHash[:])
	expectedHash2 := sha256.Sum256([]byte(chall + passwordIOS))
	expectedHashString2 := hex.EncodeToString(expectedHash2[:])

	if bytes.Equal([]byte(expectedHashString), []byte(hash)) || bytes.Equal([]byte(expectedHashString2), []byte(hash)) {
		var data struct {
			Username string `json:"username"`
			Password string `json:"password"`
			Totp     string `json:"totp"`
		}

		if err := c.ShouldBindJSON(&data); err != nil {
			c.JSON(http.StatusBadRequest, gin.H{"message": "Invalid request body"})
			return
		}

		data.Username = strings.ToLower(data.Username)

		if !validateEmail(data.Username) {
			c.JSON(http.StatusBadRequest, gin.H{"message": "Invalid request body"})
			return
		}

		user, err2 := getUser(data.Username)
		if err2 != nil {
			c.JSON(http.StatusNotFound, gin.H{"message": "Invalid credentials"})
			return
		}

		if !user.Verified {
			c.JSON(http.StatusNotFound, gin.H{"message": "Invalid credentials"})
			return
		}

		if user.Totp != "" {
			valid, err := totp.ValidateCustom(data.Totp, user.Totp, time.Now().UTC(), totp.ValidateOpts{
				Period:    30,
				Skew:      0,
				Digits:    6,
				Algorithm: otp.AlgorithmSHA512,
			})
			if err != nil {
				c.JSON(http.StatusMethodNotAllowed, gin.H{"message": "Invalid credentials"})
				return
			}
			if !valid {
				c.JSON(http.StatusMethodNotAllowed, gin.H{"message": "Invalid credentials"})
				return
			}
		}

		match := CheckPasswordHash(data.Password, user.Password)
		if !match {
			c.JSON(http.StatusNotFound, gin.H{"message": "Invalid credentials"})
			return
		}

		token, err3 := generateToken(data.Username)
		if err3 != nil {
			c.JSON(http.StatusNotFound, gin.H{"message": "Invalid credentials"})
			return
		}

		c.JSON(http.StatusOK, gin.H{"message": "Logged in", "token": token})
	} else {
		c.JSON(http.StatusBadRequest, gin.H{"message": "Invalid request body"})
		return
	}
}

func registerHandler(c *gin.Context) {
	if c.Request.Method == "GET" {
		challenge, err := generateRandomKey(32)
		if err != nil {
			c.JSON(http.StatusBadRequest, gin.H{"message": "Invalid request body"})
			return
		}
		c.JSON(http.StatusOK, gin.H{"challenge": hex.EncodeToString(challenge)})
		return
	}

	chall := c.GetHeader("X-Auth-Challenge")
	hash := c.GetHeader("X-Auth-Hash")
	if hash == "" {
		c.JSON(http.StatusBadRequest, gin.H{"message": "Invalid request body"})
		return
	}

	expectedHash := sha256.Sum256([]byte(chall + passwordDesktop))
	expectedHashString := hex.EncodeToString(expectedHash[:])
	expectedHash2 := sha256.Sum256([]byte(chall + passwordIOS))
	expectedHashString2 := hex.EncodeToString(expectedHash2[:])

	if bytes.Equal([]byte(expectedHashString), []byte(hash)) || bytes.Equal([]byte(expectedHashString2), []byte(hash)) {
		var data User

		if err := c.ShouldBindJSON(&data); err != nil {
			c.JSON(http.StatusBadRequest, gin.H{"message": "Invalid request body"})
			return
		}

		data.Username = strings.ToLower(data.Username)

		if !validateEmail(data.Username) {
			c.JSON(http.StatusBadRequest, gin.H{"message": "Invalid request body"})
			return
		}

		if !isPasswordSecure(data.Password) {
			c.JSON(http.StatusBadRequest, gin.H{"message": "Invalid request body"})
			return
		}

		hashedPassword, err := bcrypt.GenerateFromPassword([]byte(data.Password), 14)
		if err != nil {
			c.AbortWithStatus(http.StatusNotFound)
			return
		}

		data.Password = string(hashedPassword)
		data.Verified = false
		data.Code = generateCode()

		err2 := addUser(data)
		if err2 != nil {
			c.JSON(http.StatusCreated, gin.H{"message": "Check your email for verification"})
			return
		}
		sendEmail(data.Username, data.Code)
		c.JSON(http.StatusCreated, gin.H{"message": "Check your email for verification"})
	} else {
		c.JSON(http.StatusBadRequest, gin.H{"message": "Invalid request body"})
		return
	}
}

func verifyHandler(c *gin.Context) {
	code := c.Query("code")
	if code == "" {
		c.HTML(http.StatusBadRequest, "verified.html", gin.H{"message": "Invalid query parameter"})
		return
	}
	user, err := getUserByCode(code)
	if err != nil {
		c.HTML(http.StatusNotFound, "verified.html", gin.H{"message": "Account already verified"})
		return
	}
	if user.Verified {
		c.HTML(http.StatusNotFound, "verified.html", gin.H{"message": "Account already verified"})
		return
	}
	err2 := setVerified(user.Username)
	if err2 != nil {
		c.HTML(http.StatusNotFound, "verified.html", gin.H{"message": "Account already verified"})
		return
	}
	c.HTML(http.StatusOK, "verified.html", gin.H{"message": "Account verified"})
}

func otpGenerateHandler(c *gin.Context) {
	username := c.GetString("username")
	user, err3 := getUser(username)
	if err3 != nil {
		c.JSON(http.StatusNotFound, gin.H{"message": "Failed to generate QR code"})
		return
	}
	if !user.Verified {
		c.JSON(http.StatusNotFound, gin.H{"message": "Failed to generate QR code"})
		return
	}
	key, err := totp.Generate(totp.GenerateOpts{
		Issuer:      "Finalpass",
		AccountName: username,
		Algorithm:   otp.AlgorithmSHA512,
		Digits:      6,
		Period:      30,
	})
	if err != nil {
		log.Println(err)
		c.JSON(http.StatusNotFound, gin.H{"message": "Failed to generate QR code"})
		return
	}
	err2 := setTotp(username, key.Secret())
	if err2 != nil {
		log.Println(err2)
		c.JSON(http.StatusNotFound, gin.H{"message": "Failed to generate QR code"})
		return
	}
	c.JSON(http.StatusOK, gin.H{"message": "Generated QR code", "qr": key.String()})
}

func otpRemoveHandler(c *gin.Context) {
	username := c.GetString("username")
	user, err3 := getUser(username)
	if err3 != nil {
		c.JSON(http.StatusNotFound, gin.H{"message": "Failed to remove OTP"})
		return
	}
	if !user.Verified {
		c.JSON(http.StatusNotFound, gin.H{"message": "Failed to remove OTP"})
		return
	}
	err2 := setTotp(username, "")
	if err2 != nil {
		log.Println(err2)
		c.JSON(http.StatusNotFound, gin.H{"message": "Failed to remove OTP"})
		return
	}
	c.JSON(http.StatusOK, gin.H{"message": "OTP removed"})
}

func passwordHandler(c *gin.Context) {
	username := c.GetString("username")
	user, err3 := getUser(username)
	if err3 != nil {
		c.JSON(http.StatusNotFound, gin.H{"message": "Failed to change password"})
		return
	}
	if !user.Verified {
		c.JSON(http.StatusNotFound, gin.H{"message": "Failed to change password"})
		return
	}
	var data struct {
		CurrentPassword string `json:"currentpassword"`
		NewPassword     string `json:"newpassword"`
	}
	if err := c.ShouldBindJSON(&data); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"message": "Invalid request body"})
		return
	}
	match := CheckPasswordHash(data.CurrentPassword, user.Password)
	if !match {
		c.JSON(http.StatusNotFound, gin.H{"message": "Invalid credentials"})
		return
	}
	hashedPassword, err4 := bcrypt.GenerateFromPassword([]byte(data.NewPassword), 14)
	if err4 != nil {
		c.AbortWithStatus(http.StatusNotFound)
		return
	}
	user.Password = string(hashedPassword)
	err2 := updateUser(user)
	if err2 != nil {
		c.JSON(http.StatusNotFound, gin.H{"message": "Failed to change password"})
		return
	}
	c.JSON(http.StatusOK, gin.H{"message": "Password changed"})
}

func settingsHandler(c *gin.Context) {
	username := c.GetString("username")
	user, err3 := getUser(username)
	if err3 != nil {
		c.JSON(http.StatusNotFound, gin.H{"message": "Failed to get user"})
		return
	}
	if !user.Verified {
		c.JSON(http.StatusNotFound, gin.H{"message": "Failed to get user"})
		return
	}
	c.JSON(http.StatusOK, gin.H{"message": "Account found", "verified": user.Verified, "totp": user.Totp != ""})
}

func saveHandler(c *gin.Context) {
	content := c.GetHeader("Content-Type")
	if !strings.Contains(content, "multipart/form-data; boundary=") {
		c.JSON(http.StatusBadRequest, gin.H{"message": "Invalid request body"})
		return
	}
	username := c.GetString("username")
	user, err3 := getUser(username)
	if err3 != nil {
		c.JSON(http.StatusNotFound, gin.H{"message": "Failed to get user"})
		return
	}
	if !user.Verified {
		c.JSON(http.StatusNotFound, gin.H{"message": "Failed to get user"})
		return
	}
	form, err := c.MultipartForm()
	if err != nil {
		c.JSON(http.StatusNotFound, gin.H{"message": "Invalid request body"})
		return
	}
	file := form.File["file"]
	if len(file) != 1 {
		c.JSON(http.StatusNotFound, gin.H{"message": "Invalid request body"})
		return
	}
	maxFileSize := int64(10 * 1024 * 1024)
	if file[0].Size > maxFileSize {
		c.JSON(http.StatusMethodNotAllowed, gin.H{"message": "Max file size exceeded"})
		return
	}
	if err2 := c.SaveUploadedFile(file[0], "files/"+username+".db"); err2 != nil {
		c.JSON(http.StatusNotFound, gin.H{"message": "Invalid request body"})
		return
	}
	c.JSON(http.StatusOK, gin.H{"message": "Database saved"})
}

func syncHandler(c *gin.Context) {
	username := c.GetString("username")
	user, err3 := getUser(username)
	if err3 != nil {
		c.JSON(http.StatusNotFound, gin.H{"message": "Failed to get user"})
		return
	}
	if !user.Verified {
		c.JSON(http.StatusNotFound, gin.H{"message": "Failed to get user"})
		return
	}
	file, err := os.ReadFile("files/" + username + ".db")
	if err != nil {
		c.JSON(http.StatusNotFound, gin.H{"message": "Failed to sync database"})
		return
	}
	c.Data(http.StatusOK, "application/octet-stream", file)
}

func terminateHandler(c *gin.Context) {
	username := c.GetString("username")
	user, err3 := getUser(username)
	if err3 != nil {
		c.JSON(http.StatusNotFound, gin.H{"message": "Failed to get user"})
		return
	}
	if !user.Verified {
		c.JSON(http.StatusNotFound, gin.H{"message": "Failed to get user"})
		return
	}
	err := os.Remove("files/" + username + ".db")
	if err != nil {
		c.JSON(http.StatusNotFound, gin.H{"message": "Failed to terminate account"})
		return
	}
	err2 := removeUser(username)
	if err2 != nil {
		log.Println(err2)
	}
	c.JSON(http.StatusOK, gin.H{"message": "Account terminated"})
}

func validateEmail(email string) bool {
	emailRegex := regexp.MustCompile(`^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$`)
	return emailRegex.MatchString(email)
}

func sendEmail(toEmail string, code string) {
	password := emailPassword
	to := []string{
		toEmail,
	}
	smtpHost := "smtp.gmail.com"
	smtpPort := "587"
	message := []byte(fmt.Sprintf("Subject: Finalpass Email Verification\n\nThis email was sent by Finalpass\n\nClick this link to verify your account:\n\n%s/verify?code=%s\n\nIf you did not request this, please ignore this email.", url, code))
	auth := smtp.PlainAuth("", email, password, smtpHost)
	err := smtp.SendMail(smtpHost+":"+smtpPort, auth, email, to, message)
	if err != nil {
		log.Println(err)
		return
	}
	log.Println("Email Sent Successfully!")
}

func isPasswordSecure(password string) bool {
	if len(password) < 8 {
		return false
	}

	hasUppercase := false
	for _, character := range password {
		if character >= 'A' && character <= 'Z' {
			hasUppercase = true
			break
		}
	}
	if !hasUppercase {
		return false
	}

	hasDigit := false
	for _, character := range password {
		if character >= '0' && character <= '9' {
			hasDigit = true
			break
		}
	}
	if !hasDigit {
		return false
	}

	hasSpecialCharacter := false
	for _, character := range password {
		if (character >= '!' && character <= '/') ||
			(character >= ':' && character <= '@') ||
			(character >= '[' && character <= '`') ||
			(character >= '{' && character <= '~') {
			hasSpecialCharacter = true
			break
		}
	}
	return hasSpecialCharacter
}

func main() {
	err := godotenv.Load("config.env")
	if err != nil {
		log.Fatal("Error loading config.env file")
	}
	passwordDesktop = os.Getenv("PASSWORD_DESKTOP")
	if passwordDesktop == "" {
		log.Fatal("PASSWORD_DESKTOP environment variable is not set")
	}
	passwordIOS = os.Getenv("PASSWORD_IOS")
	if passwordIOS == "" {
		log.Fatal("PASSWORD_IOS environment variable is not set")
	}
	email = os.Getenv("EMAIL")
	if email == "" {
		log.Fatal("EMAIL environment variable is not set")
	}
	emailPassword = os.Getenv("EMAIL_PASSWORD")
	if emailPassword == "" {
		log.Fatal("EMAIL_PASSWORD environment variable is not set")
	}
	url = os.Getenv("URL")
	if url == "" {
		log.Fatal("URL environment variable is not set")
	}

	init := initDB()
	if init != nil {
		log.Println("Error initializing database:", init)
		return
	}

	jwtKey, err := generateRandomKey(32)
	if err != nil {
		log.Println("Error generating jwtKey:", err)
		return
	}

	encodedKey := base64.StdEncoding.EncodeToString(jwtKey)

	secretKey = []byte(encodedKey)

	router := gin.Default()

	router.LoadHTMLGlob("templates/*")

	router.Use(cors.Default())

	router.POST("/login", loginHandler)
	router.GET("/login", loginHandler)
	router.POST("/register", registerHandler)
	router.GET("/register", registerHandler)
	router.GET("/verify", verifyHandler)

	auth := router.Group("/")
	auth.Use(authMiddleware())
	{
		auth.GET("/user/settings", settingsHandler)
		auth.POST("/otp/generate", otpGenerateHandler)
		auth.POST("/otp/remove", otpRemoveHandler)
		auth.POST("/user/password", passwordHandler)
		auth.POST("/user/save", saveHandler)
		auth.GET("/user/sync", syncHandler)
		auth.POST("/user/terminate", terminateHandler)
	}

	err2 := router.RunTLS(":3000", "fullchain.pem", "privkey.pem")
	if err2 != nil {
		log.Println("ListenAndServe: ", err2)
		router.Run(":3000")
	}

}

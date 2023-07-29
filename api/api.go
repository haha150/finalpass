package main

import (
	"crypto/rand"
	"encoding/base64"
	"log"
	"net/http"
	"time"

	"github.com/dgrijalva/jwt-go"
	"github.com/gin-contrib/cors"
	"github.com/gin-gonic/gin"
	"github.com/google/uuid"
	"github.com/pquerna/otp"
	"github.com/pquerna/otp/totp"
	"golang.org/x/crypto/bcrypt"
	"gorm.io/driver/sqlite"
	"gorm.io/gorm"
)

var file = "db/auth.db"
var secretKey = []byte("secret")

type User struct {
	gorm.Model
	ID       int
	Username string `json:"username" gorm:"unique"`
	Password string `json:"password" gorm:"not null"`
	Verified bool   `json:"verified" gorm:"default:false"`
	Code     string `json:"code" gorm:"unique"`
	Totp     string `json:"totp" gorm:"unique"`
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
	var data struct {
		Username string `json:"username"`
		Password string `json:"password"`
		Totp     string `json:"totp"`
	}

	if err := c.ShouldBindJSON(&data); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"message": "Invalid request body"})
		return
	}

	user, err2 := getUser(data.Username)
	if err2 != nil {
		c.JSON(http.StatusUnauthorized, gin.H{"message": "Invalid credentials"})
		return
	}

	if !user.Verified {
		c.JSON(http.StatusUnauthorized, gin.H{"message": "Invalid credentials"})
		return
	}

	if user.Totp != "" {
		valid := totp.Validate(data.Totp, user.Totp)
		if !valid {
			c.JSON(http.StatusUnauthorized, gin.H{"message": "Invalid credentials"})
			return
		}
	}

	match := CheckPasswordHash(data.Password, user.Password)
	if !match {
		c.JSON(http.StatusUnauthorized, gin.H{"message": "Invalid credentials"})
		return
	}

	token, err3 := generateToken(data.Username)
	if err3 != nil {
		c.JSON(http.StatusUnauthorized, gin.H{"message": "Invalid credentials"})
		return
	}

	c.JSON(http.StatusOK, gin.H{"token": token})
}

func registerHandler(c *gin.Context) {
	var data User

	if err := c.ShouldBindJSON(&data); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"message": "Invalid request body"})
		return
	}

	hashedPassword, err := bcrypt.GenerateFromPassword([]byte(data.Password), 16)
	if err != nil {
		c.AbortWithStatus(http.StatusInternalServerError)
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
	c.JSON(http.StatusCreated, gin.H{"message": "Check your email for verification"})
}

func verifyHandler(c *gin.Context) {
	code := c.Query("code")
	if code == "" {
		c.JSON(http.StatusBadRequest, gin.H{"message": "Invalid query parameter"})
		return
	}
	user, err := getUserByCode(code)
	if err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"message": "User already verified"})
		return
	}
	if user.Verified {
		c.JSON(http.StatusBadRequest, gin.H{"message": "User already verified"})
		return
	}
	err2 := setVerified(user.Username)
	if err2 != nil {
		c.JSON(http.StatusBadRequest, gin.H{"message": "User already verified"})
		return
	}
	c.JSON(http.StatusOK, gin.H{"message": "User verified"})
}

func otpGenerateHandler(c *gin.Context) {
	username := c.GetString("username")
	user, err3 := getUser(username)
	if err3 != nil {
		c.JSON(http.StatusBadRequest, gin.H{"message": "Failed to generate QR code"})
		return
	}
	if !user.Verified {
		c.JSON(http.StatusBadRequest, gin.H{"message": "Failed to generate QR code"})
		return
	}
	key, err := totp.Generate(totp.GenerateOpts{
		Issuer:      "Finalpass",
		AccountName: username,
		Algorithm:   otp.AlgorithmSHA512,
	})
	if err != nil {
		log.Println(err)
		c.JSON(http.StatusBadRequest, gin.H{"message": "Failed to generate QR code"})
		return
	}
	err2 := setTotp(username, key.Secret())
	if err2 != nil {
		log.Println(err2)
		c.JSON(http.StatusBadRequest, gin.H{"message": "Failed to generate QR code"})
		return
	}
	c.JSON(http.StatusOK, gin.H{"message": "Generated QR code", "qr": key.String()})
}

func main() {
	init := initDB()
	if init != nil {
		log.Println("Error initializing database:", init)
		return
	}

	keyLength := 32

	jwtKey, err := generateRandomKey(keyLength)
	if err != nil {
		log.Println("Error generating jwtKey:", err)
		return
	}

	encodedKey := base64.StdEncoding.EncodeToString(jwtKey)

	secretKey = []byte(encodedKey)

	router := gin.Default()

	router.Use(cors.Default())

	router.POST("/login", loginHandler)
	router.POST("/register", registerHandler)
	router.GET("/verify", verifyHandler)

	auth := router.Group("/")
	auth.Use(authMiddleware())
	{
		auth.GET("/otp/generate", otpGenerateHandler)
	}

	err2 := router.RunTLS(":3000", "certificate.crt", "private.key")
	if err2 != nil {
		log.Println("ListenAndServe: ", err2)
		router.Run(":3000")
	}

}

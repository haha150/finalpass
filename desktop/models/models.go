package models

var Url string = ""
var Password string = ""

type Database struct {
	ID           int           `gorm:"primaryKey"`
	Name         string        `gorm:"unique;not null"`
	Created_at   string        `gorm:"not null"`
	Updated_at   string        `gorm:"not null"`
	SecretGroups []SecretGroup `gorm:"foreignkey:DatabaseID"`
}

type SecretGroup struct {
	ID         int    `gorm:"primaryKey"`
	Name       string `gorm:"not null"`
	DatabaseID int
	Created_at string   `gorm:"not null"`
	Updated_at string   `gorm:"not null"`
	Secrets    []Secret `gorm:"foreignkey:SecretGroupID"`
}

type Secret struct {
	ID            int    `gorm:"primaryKey"`
	Username      string `gorm:"not null"`
	Password      []byte `gorm:"not null"`
	Title         string
	Description   string
	URL           string
	Created_at    string `gorm:"not null"`
	Updated_at    string `gorm:"not null"`
	SecretGroupID int
}

type Configuration struct {
	Database string
}

type User struct {
	Email    string
	Token    string
	Verified bool
	Totp     bool
}

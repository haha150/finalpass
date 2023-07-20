package models

import (
	"log"
	"os"
	"path/filepath"
)

// Path is the path to the database file
var Path string

// Init initializes the database
func Init() {
	home, err := os.UserHomeDir()
	if err != nil {
		log.Fatal(err)
	}

	Path = filepath.Join(home, ".local", "share", "passman", "passman.db")

	if _, err := os.Stat(Path); os.IsNotExist(err) {
		os.MkdirAll(filepath.Dir(Path), os.ModePerm)
		os.Create(Path)
	}
}

// GetCategories returns all categories
func GetCategories() []string {
	var categories []string

	db := Open()
	defer db.Close()

	rows, err := db.Query("SELECT name FROM categories")
	if err != nil {
		log.Fatal(err)
	}
	defer rows.Close()

	for rows.Next() {
		var category string
		err := rows.Scan(&category)
		if err != nil {
			log.Fatal(err)
		}
		categories = append(categories, category)
	}

	return categories
}

package main

import (
	"log"
	"os"

	"desktop/models"
	"desktop/views"

	_ "github.com/joho/godotenv/autoload"
	"github.com/therecipe/qt/core"
	"github.com/therecipe/qt/gui"
	"github.com/therecipe/qt/widgets"
)

func main() {
	log.Println("Start application")
	password := os.Getenv("PASSWORD")
	if password == "" {
		log.Fatal("PASSWORD environment variable is not set")
	}
	url := os.Getenv("URL")
	if url == "" {
		log.Fatal("URL environment variable is not set")
	}
	models.Url = url
	models.Password = password
	app := widgets.NewQApplication(len(os.Args), os.Args)
	window := widgets.NewQMainWindow(nil, 0)
	icon := gui.NewQIcon5("icons/main.svg")
	window.SetWindowIcon(icon)
	window.SetMinimumSize2(800, 600)
	window.SetWindowTitle("Finalpass")
	menu := views.CreateMenu()
	window.SetMenuBar(menu)
	tool := views.CreateToolBar()
	window.AddToolBar(core.Qt__TopToolBarArea, tool)
	central := widgets.NewQWidget(nil, 0)
	mainLayout := widgets.NewQVBoxLayout2(central)
	side := views.CreateSideMenu()
	main := views.CreateMain()
	splitter := widgets.NewQSplitter(nil)
	splitter.AddWidget(side)
	splitter.AddWidget(main)
	mainLayout.AddWidget(splitter, 0, 0)
	window.SetCentralWidget(central)
	window.Show()
	views.Inits()
	app.Exec()
}

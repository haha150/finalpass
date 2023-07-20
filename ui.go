package main

import (
	"fmt"
	"os"

	"password-manager/controller"
	"password-manager/security"

	"github.com/therecipe/qt/core"
	"github.com/therecipe/qt/gui"
	"github.com/therecipe/qt/widgets"
)

func createMenu() *widgets.QMenuBar {
	menu := widgets.NewQMenuBar(nil)
	file := menu.AddMenu2("File")

	// create action for menu File
	newDatabase := widgets.NewQAction(nil)
	newDatabase.SetIcon(gui.NewQIcon5("icons/category.svg"))
	newDatabase.SetText("New database")
	newDatabase.ConnectTriggered(func(bool) {
		fmt.Println("New database")
	})

	file.InsertAction(nil, newDatabase)

	return menu
}

func createToolBar() *widgets.QToolBar {
	tool := widgets.NewQToolBar2(nil)
	tool.SetIconSize(core.NewQSize2(32, 32))
	tool.SetStyleSheet("background-color: #FFFFFF;")
	tool.SetFixedHeight(50)

	save := widgets.NewQAction(nil)
	save.SetIcon(gui.NewQIcon5("icons/save.svg"))
	save.SetToolTip("Save")

	category := widgets.NewQAction(nil)
	category.SetIcon(gui.NewQIcon5("icons/category.svg"))
	category.SetToolTip("Category")

	add := widgets.NewQAction(nil)
	add.SetIcon(gui.NewQIcon5("icons/add.svg"))
	add.SetToolTip("Add")

	remove := widgets.NewQAction(nil)
	remove.SetIcon(gui.NewQIcon5("icons/remove.svg"))
	remove.SetToolTip("Remove")

	tool.InsertAction(nil, save)
	tool.InsertAction(nil, category)
	tool.InsertAction(nil, add)
	tool.InsertAction(nil, remove)

	return tool
}

func createSideMenu() *widgets.QWidget {
	widget := widgets.NewQWidget(nil, 0)
	widget.SetFixedWidth(100)
	widget.SetMaximumWidth(200)

	tree := widgets.NewQTreeWidget(nil)
	tree.SetHeaderHidden(true)
	tree.SetColumnCount(1)
	tree.SetColumnWidth(0, 100)
	tree.SetIndentation(0)
	tree.SetFocusPolicy(1)
	tree.SetSelectionMode(0)
	tree.SetAnimated(true)
	tree.SetUniformRowHeights(true)
	tree.SetRootIsDecorated(false)
	tree.SetItemsExpandable(false)
	tree.SetHorizontalScrollBarPolicy(1)
	tree.SetVerticalScrollBarPolicy(1)
	tree.SetAutoScroll(true)
	tree.SetAutoScrollMargin(10)

	item1 := widgets.NewQTreeWidgetItem2([]string{"Database"}, 0)
	tree.AddTopLevelItem(item1)

	layout := widgets.NewQHBoxLayout2(widget)
	layout.SetContentsMargins(0, 0, 0, 0)
	layout.SetSpacing(0)
	layout.AddWidget(tree, 0, 0)

	return widget
}

func createMain() *widgets.QWidget {
	widget := widgets.NewQWidget(nil, 0)
	widget.SetStyleSheet("background-color: #FFFFFF;")

	l1 := widgets.NewQLabel(nil, 0)
	l1.SetText("Home")

	layout := widgets.NewQHBoxLayout2(widget)
	layout.AddWidget(l1, 0, 0)

	return widget
}

func main() {
	controller.Init()
	//log.SetFlags(0)
	//log.SetOutput(ioutil.Discard)
	app := widgets.NewQApplication(len(os.Args), os.Args)
	window := widgets.NewQMainWindow(nil, 0)
	icon := gui.NewQIcon5("icons/pepega.png")
	window.SetWindowIcon(icon)
	window.SetMinimumSize2(800, 600)
	window.SetWindowTitle("Password manager")
	menu := createMenu()
	window.SetMenuBar(menu)
	tool := createToolBar()
	window.AddToolBar(core.Qt__TopToolBarArea, tool)
	central := widgets.NewQWidget(nil, 0)
	mainLayout := widgets.NewQVBoxLayout2(central)
	side := createSideMenu()
	main := createMain()
	splitter := widgets.NewQSplitter(nil)
	splitter.AddWidget(side)
	splitter.AddWidget(main)
	security.Crypted()
	mainLayout.AddWidget(splitter, 0, 0)
	window.SetCentralWidget(central)
	window.Show()
	app.Exec()
}

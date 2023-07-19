package main

import (
	"crypto/rand"
	"fmt"
	"io"
	"log"
	"golang.org/x/crypto/chacha20"
	"io/ioutil"
	"os"
	// "strings"

	// "github.com/therecipe/qt/core"
	"github.com/therecipe/qt/widgets"
	"github.com/therecipe/qt/gui"
)

func crypted() {
	// Create a 256-bit key and 96-bit nonce for encryption
	key := make([]byte, 32)
	nonce := make([]byte, 12)
	if _, err := io.ReadFull(rand.Reader, key); err != nil {
		log.Fatal(err)
	}
	if _, err := io.ReadFull(rand.Reader, nonce); err != nil {
		log.Fatal(err)
	}

	plaintext := []byte("Hello, World!")

	// Create a new ChaCha20 cipher
	c, err := chacha20.NewUnauthenticatedCipher(key, nonce)
	if err != nil {
		log.Fatal(err)
	}

	// Encrypt the plaintext
	ciphertext := make([]byte, len(plaintext))
	c.XORKeyStream(ciphertext, plaintext)

	fmt.Printf("Ciphertext: %x\n", ciphertext)

	// Create a new ChaCha20 cipher for decryption
	decryptionCipher, err := chacha20.NewUnauthenticatedCipher(key, nonce)
	if err != nil {
		log.Fatal(err)
	}

	// Decrypt the ciphertext
	decrypted := make([]byte, len(ciphertext))
	decryptionCipher.XORKeyStream(decrypted, ciphertext)

	fmt.Printf("Decrypted: %s\n", decrypted)
}

func createMenu() *widgets.QMenuBar {
	menu := widgets.NewQMenuBar(nil)
	menu.AddMenu2("File")
	return menu
}

func createSideMenu() *widgets.QWidget {
	widget := widgets.NewQWidget(nil, 0)
	widget.SetFixedWidth(100)
	widget.SetStyleSheet("background-color: #F0F0F0;")

	tree := widgets.NewQTreeWidget(nil)
	tree.SetHeaderHidden(true)
	tree.SetStyleSheet("background-color: #F0F0F0;")
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
	layout.AddWidget(tree, 0, 0)

	return widget
}

func createLine() *widgets.QWidget {
	widget := widgets.NewQWidget(nil, 0)
	widget.SetFixedWidth(1)
	widget.SetStyleSheet("background-color: #AAAAAA;")
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
	log.SetFlags(0)
	log.SetOutput(ioutil.Discard)
	app := widgets.NewQApplication(len(os.Args), os.Args)
	window := widgets.NewQMainWindow(nil, 0)
	icon := gui.NewQIcon5("pepega.png")
	window.SetWindowIcon(icon)
	window.SetMinimumSize2(800, 600)
	window.SetWindowTitle("Password manager")
	menu := createMenu()
	window.SetMenuBar(menu)
	central := widgets.NewQWidget(nil, 0)
	mainLayout := widgets.NewQVBoxLayout2(central)
	layout := widgets.NewQHBoxLayout2(nil)
	side := createSideMenu()
	line := createLine()
	main := createMain()
	layout.AddWidget(side, 0, 0)
	layout.AddWidget(line, 0, 0)
	layout.AddWidget(main, 0, 0)
	window.SetCentralWidget(central)
	mainLayout.AddLayout(layout, 0)
	window.Show()
	app.Exec()
}
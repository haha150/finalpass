package security

import (
	cryptorand "crypto/rand"
	"crypto/sha256"
	"fmt"
	"log"
	"math/rand"
	"os"

	"golang.org/x/crypto/chacha20poly1305"
)

func EncryptText(password string, plaintext string) ([]byte, error) {
	h := sha256.New()
	h.Write([]byte(password))
	key := h.Sum(nil)
	dataToEncrypt := []byte(plaintext)

	aead, err := chacha20poly1305.New(key[:])
	if err != nil {
		return nil, err
	}

	nonce := make([]byte, aead.NonceSize())
	if _, err := cryptorand.Read(nonce); err != nil {
		return nil, err
	}

	ciphertext := aead.Seal(nil, nonce, dataToEncrypt, nil)
	return append(nonce, ciphertext...), nil
}

func DecryptText(password string, ciphertext []byte) ([]byte, error) {
	h := sha256.New()
	h.Write([]byte(password))
	key := h.Sum(nil)

	aead, err := chacha20poly1305.New(key[:])
	if err != nil {
		return nil, err
	}

	nonceSize := aead.NonceSize()
	if len(ciphertext) < nonceSize+aead.Overhead() {
		return nil, fmt.Errorf("ciphertext is too short")
	}

	nonce := ciphertext[:nonceSize]
	decryptedData, err := aead.Open(nil, nonce, ciphertext[nonceSize:], nil)
	if err != nil {
		return nil, err
	}

	return decryptedData, nil
}

func EncryptFile(plaintext_file string, password string, tmp_file string) bool {
	encrypted := encryptFile(plaintext_file, password, tmp_file)
	err := os.Remove(tmp_file)
	if err != nil {
		log.Println("Error when removing file.")
	}
	if !encrypted {
		return false
	}
	return true
}

func encryptFile(plaintext_file string, password string, tmp_file string) bool {
	outfile, err := os.OpenFile(plaintext_file, os.O_RDWR|os.O_CREATE, 0666)
	if err != nil {
		log.Println("Error when opening/creating output file.")
		return false
	}
	defer outfile.Close()

	infile, err := os.ReadFile(tmp_file)
	if err != nil {
		log.Println("Error when opening input file.")
		return false
	}

	dataToEncrypt := []byte(infile)

	s, e := EncryptText(password, string(dataToEncrypt))
	if e != nil {
		log.Println("Error when decrypting text.", e)
		return false
	}

	outfile.Write(s)
	return true
}

func DecryptFile(file string, password string, decrypted_file string) bool {
	infile, err := os.ReadFile(file)
	if err != nil {
		log.Println("Error when opening input file.")
		return false
	}

	ciphertext := []byte(infile)

	outfile, err := os.OpenFile(decrypted_file, os.O_RDWR|os.O_CREATE, 0666)
	if err != nil {
		log.Println("Error when opening output file.")
		return false
	}
	defer outfile.Close()

	decryptedData, err := DecryptText(password, ciphertext)
	if err != nil {
		log.Println("Error when decrypting text.", err)
		return false
	}

	outfile.Write([]byte(decryptedData))

	return true
}

func GenerateStrongPassword(length int) string {
	characters := "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789~!@#$%^&*()_+-={}|[]?<>,.:;"
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

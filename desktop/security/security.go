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
	// h := sha256.New()
	// h.Write([]byte(password))
	// key := h.Sum(nil)

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

	// aead, err := chacha20poly1305.New(key[:])
	// if err != nil {
	// 	return false
	// }

	// nonce := make([]byte, aead.NonceSize())
	// if _, err := cryptorand.Read(nonce); err != nil {
	// 	return false
	// }

	s, e := EncryptText(password, string(dataToEncrypt))
	if e != nil {
		log.Println("Error when decrypting text.", e)
		return false
	}

	// ciphertext := aead.Seal(nil, nonce, dataToEncrypt, nil)
	// ciphertext = append(nonce, ciphertext...)
	outfile.Write(s)
	return true
}

func DecryptFile(file string, password string, decrypted_file string) bool {
	// h := sha256.New()
	// h.Write([]byte(password))
	// key := h.Sum(nil)

	infile, err := os.ReadFile(file)
	if err != nil {
		log.Println("Error when opening input file.")
		return false
	}

	ciphertext := []byte(infile)

	// aead, err := chacha20poly1305.NewX(key)
	// if err != nil {
	// 	log.Println("Error when creating cipher.")
	// 	return false
	// }

	outfile, err := os.OpenFile(decrypted_file, os.O_RDWR|os.O_CREATE, 0666)
	if err != nil {
		log.Println("Error when opening output file.")
		return false
	}
	defer outfile.Close()

	// nonceSize := aead.NonceSize()
	// if len(ciphertext) < nonceSize+aead.Overhead() {
	// 	return false
	// }

	// nonce := ciphertext[:nonceSize]
	// decryptedData, err := aead.Open(nil, nonce, ciphertext[nonceSize:], nil)
	// if err != nil {
	// 	return false
	// }

	decryptedData, err := DecryptText(password, ciphertext)
	if err != nil {
		log.Println("Error when decrypting text.", err)
		return false
	}

	outfile.Write([]byte(decryptedData))

	return true
}

// func EncryptText(password string, plaintext string) ([]byte, error) {
// 	paswd := []byte(password)
// 	salt := make([]byte, SaltSize)
// 	if n, err := cryptorand.Read(salt); err != nil || n != SaltSize {
// 		log.Println("Error when generating radom salt.")
// 		return nil, err
// 	}
// 	key := argon2.IDKey(paswd, salt, KeyTime, KeyMemory, KeyThreads, KeySize)
// 	aead, err := chacha20poly1305.NewX(key)
// 	if err != nil {
// 		log.Println("Error when creating cipher.")
// 		return nil, err
// 	}
// 	n := len(plaintext)
// 	nonce := make([]byte, aead.NonceSize(), aead.NonceSize()+n+aead.Overhead())
// 	if m, err := cryptorand.Read(nonce); err != nil || m != aead.NonceSize() {
// 		log.Println("Error when generating random nonce :", err)
// 		log.Println("Generated nonce is of following size. m : ", m)
// 		return nil, err
// 	}
// 	ad_counter := 0
// 	encryptedMsgfull := make([]byte, 0)
// 	ciphertext := aead.Seal(nonce, nonce, []byte(plaintext), []byte(string(ad_counter)))
// 	encryptedMsgfull = append(encryptedMsgfull, salt...)
// 	encryptedMsgfull = append(encryptedMsgfull, ciphertext...)
// 	return encryptedMsgfull, nil
// }

// func EncryptFile(plaintext_file string, password string, tmp_file string) bool {
// 	encrypted := encryptFile(plaintext_file, password, tmp_file)
// 	err := os.Remove(tmp_file)
// 	if err != nil {
// 		log.Println("Error when removing file.")
// 	}
// 	if !encrypted {
// 		return false
// 	}
// 	return true
// }

// func encryptFile(plaintext_file string, password string, tmp_file string) bool {
// 	paswd := []byte(password)

// 	salt := make([]byte, SaltSize)
// 	if n, err := cryptorand.Read(salt); err != nil || n != SaltSize {
// 		log.Println("Error when generating radom salt.")
// 		return false
// 	}

// 	outfile, err := os.OpenFile(plaintext_file, os.O_RDWR|os.O_CREATE, 0666)
// 	if err != nil {
// 		log.Println("Error when opening/creating output file.")
// 		return false
// 	}
// 	defer outfile.Close()

// 	outfile.Write(salt)

// 	key := argon2.IDKey(paswd, salt, KeyTime, KeyMemory, KeyThreads, KeySize)

// 	aead, err := chacha20poly1305.NewX(key)
// 	if err != nil {
// 		log.Println("Error when creating cipher.")
// 		return false
// 	}

// 	infile, err := os.Open(tmp_file)
// 	if err != nil {
// 		log.Println("Error when opening input file.")
// 		return false
// 	}
// 	defer infile.Close()

// 	buf := make([]byte, chunkSize)
// 	ad_counter := 0
// 	encryptedMsgfull := make([]byte, 0)

// 	for {
// 		n, err := infile.Read(buf)

// 		if n > 0 {
// 			nonce := make([]byte, aead.NonceSize(), aead.NonceSize()+n+aead.Overhead())
// 			if m, err := cryptorand.Read(nonce); err != nil || m != aead.NonceSize() {
// 				log.Println("Error when generating random nonce :", err)
// 				log.Println("Generated nonce is of following size. m : ", m)
// 				panic(err)
// 			}

// 			msg := buf[:n]

// 			encryptedMsg := aead.Seal(nonce, nonce, msg, []byte(string(ad_counter)))
// 			encryptedMsgfull = append(encryptedMsgfull, encryptedMsg...)
// 			ad_counter += 1
// 		}

// 		if err == io.EOF {
// 			outfile.Write(encryptedMsgfull)
// 			return true
// 		}

// 		if err != nil {
// 			log.Println("Error when reading input file chunk :", err)
// 			return false
// 		}
// 	}
// }

// func DecryptFile(file string, password string, decrypted_file string) bool {
// 	passwd := []byte(password)

// 	infile, err := os.Open(file)
// 	if err != nil {
// 		log.Println("Error when opening input file.")
// 		return false
// 	}
// 	defer infile.Close()

// 	salt := make([]byte, SaltSize)
// 	n, err := infile.Read(salt)
// 	if n != SaltSize {
// 		log.Printf("Error. Salt should be %d bytes long. salt n : %d", SaltSize, n)
// 		log.Println(err)
// 		return false
// 	}
// 	if err == io.EOF {
// 		log.Println("Encountered EOF error.")
// 		return false
// 	}
// 	if err != nil {
// 		log.Println("Error encountered :", err)
// 		return false
// 	}

// 	key := argon2.IDKey(passwd, salt, KeyTime, KeyMemory, KeyThreads, KeySize)
// 	aead, err := chacha20poly1305.NewX(key)
// 	if err != nil {
// 		log.Println("Error when creating cipher.")
// 		return false
// 	}
// 	decbufsize := aead.NonceSize() + chunkSize + aead.Overhead()

// 	outfile, err := os.OpenFile(decrypted_file, os.O_RDWR|os.O_CREATE, 0666)
// 	if err != nil {
// 		log.Println("Error when opening output file.")
// 		return false
// 	}
// 	defer outfile.Close()

// 	buf := make([]byte, decbufsize)
// 	ad_counter := 0

// 	for {
// 		n, err := infile.Read(buf)
// 		if n > 0 {
// 			encryptedMsg := buf[:n]
// 			if len(encryptedMsg) < aead.NonceSize() {
// 				log.Println("Error. Ciphertext is too short.")
// 				return false
// 			}

// 			nonce, ciphertext := encryptedMsg[:aead.NonceSize()], encryptedMsg[aead.NonceSize():]

// 			plaintext, err := aead.Open(nil, nonce, ciphertext, []byte(string(ad_counter)))
// 			if err != nil {
// 				log.Println("Error when decrypting ciphertext. May be wrong password or file is damaged.")
// 				return false
// 			}

// 			outfile.Write(plaintext)
// 		}
// 		if err == io.EOF {
// 			return true
// 		}
// 		if err != nil {
// 			log.Printf("Error encountered. Read %d bytes: %v", n, err)
// 			return false
// 		}

// 		ad_counter += 1
// 	}
// }

// func DecryptText(password string, encryptedText []byte) ([]byte, error) {
// 	paswd := []byte(password)
// 	salt := encryptedText[:SaltSize]
// 	encryptedMsg := encryptedText[SaltSize:]
// 	key := argon2.IDKey(paswd, salt, KeyTime, KeyMemory, KeyThreads, KeySize)
// 	aead, err := chacha20poly1305.NewX(key)
// 	if err != nil {
// 		log.Println("Error when creating cipher.")
// 		return nil, err
// 	}
// 	nonce := encryptedMsg[:aead.NonceSize()]
// 	ciphertext := encryptedMsg[aead.NonceSize():]
// 	ad_counter := 0
// 	plaintext, err := aead.Open(nil, nonce, ciphertext, []byte(string(ad_counter)))
// 	if err != nil {
// 		log.Println("Error when decrypting text.", err)
// 		return nil, err
// 	}
// 	return plaintext, nil
// }

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

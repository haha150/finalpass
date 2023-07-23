package security

import (
	cryptorand "crypto/rand"
	"io"
	"log"
	"math/rand"
	"os"

	"golang.org/x/crypto/argon2"
	"golang.org/x/crypto/chacha20poly1305"
)

const (
	SaltSize   = 32
	NonceSize  = 24
	KeySize    = uint32(32)
	KeyTime    = uint32(5)
	KeyMemory  = uint32(1024 * 64)
	KeyThreads = uint8(4)
	chunkSize  = 1024 * 32
)

func Enc(password string, plaintext string) {
	paswd := []byte(password)
	salt := make([]byte, SaltSize)
	if n, err := cryptorand.Read(salt); err != nil || n != SaltSize {
		log.Println("Error when generating radom salt.")
		panic(err)
	}
	key := argon2.IDKey(paswd, salt, KeyTime, KeyMemory, KeyThreads, KeySize)
	aead, err := chacha20poly1305.NewX(key)
	if err != nil {
		log.Println("Error when creating cipher.")
		panic(err)
	}
	n := len(plaintext)
	nonce := make([]byte, aead.NonceSize(), aead.NonceSize()+n+aead.Overhead())
	if m, err := cryptorand.Read(nonce); err != nil || m != aead.NonceSize() {
		log.Println("Error when generating random nonce :", err)
		log.Println("Generated nonce is of following size. m : ", m)
		panic(err)
	}
	ad_counter := 0
	ciphertext := aead.Seal(nonce, nonce, []byte(plaintext), []byte(string(ad_counter)))
	outfile, err := os.OpenFile("ab.tst.enc2", os.O_RDWR|os.O_CREATE, 0666)
	if err != nil {
		log.Println("Error when opening/creating output file.")
		panic(err)
	}
	outfile.Write(salt)
	outfile.Write(ciphertext)
	outfile.Close()
}

func EncryptFile(password string, plaintext_filename string, simple bool) []byte {
	paswd := []byte(password)

	salt := make([]byte, SaltSize)
	if n, err := cryptorand.Read(salt); err != nil || n != SaltSize {
		log.Println("Error when generating radom salt.")
		panic(err)
	}

	outfile, err := os.OpenFile(plaintext_filename+".enc", os.O_RDWR|os.O_CREATE, 0666)
	if err != nil {
		log.Println("Error when opening/creating output file.")
		panic(err)
	}
	defer outfile.Close()

	outfile.Write(salt)

	key := argon2.IDKey(paswd, salt, KeyTime, KeyMemory, KeyThreads, KeySize)

	aead, err := chacha20poly1305.NewX(key)
	if err != nil {
		log.Println("Error when creating cipher.")
		panic(err)
	}

	infile, err := os.Open(plaintext_filename)
	if err != nil {
		log.Println("Error when opening input file.")
		panic(err)
	}
	defer infile.Close()

	buf := make([]byte, chunkSize)
	ad_counter := 0
	encryptedMsgfull := make([]byte, 0)

	for {
		n, err := infile.Read(buf)

		if n > 0 {
			nonce := make([]byte, aead.NonceSize(), aead.NonceSize()+n+aead.Overhead())
			if m, err := cryptorand.Read(nonce); err != nil || m != aead.NonceSize() {
				log.Println("Error when generating random nonce :", err)
				log.Println("Generated nonce is of following size. m : ", m)
				panic(err)
			}

			msg := buf[:n]

			encryptedMsg := aead.Seal(nonce, nonce, msg, []byte(string(ad_counter)))
			encryptedMsgfull = append(encryptedMsgfull, encryptedMsg...)
			ad_counter += 1
		}

		if err == io.EOF {
			outfile.Write(encryptedMsgfull)
			break
		}

		if err != nil {
			log.Println("Error when reading input file chunk :", err)
			panic(err)
		}
	}

	return nil
}

func Decrypt(password string, ciphertext string, decryptedplaintext string) {
	passwd := []byte(password)

	infile, err := os.Open(ciphertext)
	if err != nil {
		log.Println("Error when opening input file.")
		panic(err)
	}
	defer infile.Close()

	salt := make([]byte, SaltSize)
	n, err := infile.Read(salt)
	if n != SaltSize {
		log.Printf("Error. Salt should be %d bytes long. salt n : %d", SaltSize, n)
		log.Println(err)
		panic("Generated salt is not of required length")
	}
	if err == io.EOF {
		log.Println("Encountered EOF error.")
		panic(err)
	}
	if err != nil {
		log.Println("Error encountered :", err)
		panic(err)
	}

	key := argon2.IDKey(passwd, salt, KeyTime, KeyMemory, KeyThreads, KeySize)
	aead, err := chacha20poly1305.NewX(key)
	if err != nil {
		log.Println("Error when creating cipher.")
		panic(err)
	}
	decbufsize := aead.NonceSize() + chunkSize + aead.Overhead()

	outfile, err := os.OpenFile(decryptedplaintext, os.O_RDWR|os.O_CREATE, 0666)
	if err != nil {
		log.Println("Error when opening output file.")
		panic(err)
	}
	defer outfile.Close()

	buf := make([]byte, decbufsize)
	ad_counter := 0

	for {
		n, err := infile.Read(buf)
		if n > 0 {
			encryptedMsg := buf[:n]
			if len(encryptedMsg) < aead.NonceSize() {
				log.Println("Error. Ciphertext is too short.")
				panic("Ciphertext too short")
			}

			nonce, ciphertext := encryptedMsg[:aead.NonceSize()], encryptedMsg[aead.NonceSize():]

			plaintext, err := aead.Open(nil, nonce, ciphertext, []byte(string(ad_counter)))
			if err != nil {
				log.Println("Error when decrypting ciphertext. May be wrong password or file is damaged.")
				panic(err)
			}

			outfile.Write(plaintext)
		}
		if err == io.EOF {
			break
		}
		if err != nil {
			log.Printf("Error encountered. Read %d bytes: %v", n, err)
			panic(err)
		}

		ad_counter += 1
	}
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

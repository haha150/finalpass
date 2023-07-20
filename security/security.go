package security

import (
	"crypto/rand"
	"fmt"
	"io"
	"log"

	"golang.org/x/crypto/chacha20"
)

func Crypted() {
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

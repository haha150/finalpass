global.Buffer = require('buffer').Buffer;
import 'react-native-get-random-values'
import React from 'react';
import { StyleSheet, View, ScrollView } from 'react-native';
import { Icon, ListItem, SpeedDial } from 'react-native-elements';
import * as SQLite from 'expo-sqlite';
import { secretbox, randomBytes } from "tweetnacl";

class Home extends React.Component {
    constructor(props) {
        super(props)
        this.state = {
            expanded: true,
            open: false,
            list: []
        }
        this.navigation = props.navigation;
        this.openDatabase = this.openDatabase.bind(this);
        this.db = this.openDatabase();
        this.initDb = this.initDb.bind(this);
        this.setExpanded = this.setExpanded.bind(this);
        this.fetchData = this.fetchData.bind(this);
        this.addDb = this.addDb.bind(this);
        this.addSecretGroup = this.addSecretGroup.bind(this);
        this.addSecret = this.addSecret.bind(this);
        this.setOpen = this.setOpen.bind(this);
        this.encrypt = this.encrypt.bind(this);
    }

    openDatabase() {
        if (Platform.OS === "web") {
            return {
                transaction: () => {
                    return {
                        executeSql: () => { },
                    };
                },
            };
        }

        const db = SQLite.openDatabase("finalpass.db");
        return db;
    }

    componentDidMount = () => {
        // this.db.transaction(tx => {
        //     tx.executeSql('DROP TABLE IF EXISTS database');
        //     tx.executeSql('DROP TABLE IF EXISTS secretgroup');
        //     tx.executeSql('DROP TABLE IF EXISTS secret');
        // })
        this.initDb();
        this.fetchData();
    }

    encrypt = () => {
        const nonce = randomBytes(secretbox.nonceLength);
        console.log(nonce);
        const key = randomBytes(secretbox.keyLength);
        console.log(key);
        const obj = { hello: 'world' };
        const messageUint8 = JSON.stringify(obj);
        const msg = '{ "hello": "world" }';
        console.log(messageUint8);
        // const box = secretbox(msg, nonce, key);
        // console.log(box);
    }

    initDb = () => {
        this.db.transaction(tx => {
            tx.executeSql('CREATE TABLE IF NOT EXISTS database (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP, updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP)');
            tx.executeSql('CREATE TABLE IF NOT EXISTS secretgroup (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, databaseid INTEGER, created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP, updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY (databaseid) REFERENCES database (id))');
            tx.executeSql('CREATE TABLE IF NOT EXISTS secret (id INTEGER PRIMARY KEY AUTOINCREMENT, username TEXT NOT NULL, password BLOB NOT NULL, title TEXT, description TEXT, url TEXT, secretgroupid INTEGER, created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP, updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY (secretgroupid) REFERENCES secretgroup (id))');
        });
    }

    setExpanded = (expanded) => {
        this.setState({ expanded });
    }

    setOpen = (open) => {
        this.setState({ open });
    }

    fetchData = () => {
        this.db.transaction(tx => {
            tx.executeSql('SELECT * from database', [],
                (txObj, resultSet) => {
                    console.log('Database fetched');
                    const list = [];
                    resultSet.rows._array.map((item, _) => {
                        const database = {
                            id: item.id,
                            name: item.name,
                            created_at: item.created_at,
                            updated_at: item.updated_at,
                            secretgroups: []
                        }
                        txObj.executeSql('SELECT * FROM secretgroup WHERE databaseid = ?', [item.id],
                            (_, { rows }) => {
                                rows._array.map((item, _) => {
                                    const secretgroup = {
                                        id: item.id,
                                        name: item.name,
                                        created_at: item.created_at,
                                        updated_at: item.updated_at,
                                        secrets: []
                                    }
                                    database.secretgroups.push(secretgroup);
                                    txObj.executeSql('SELECT * FROM secret WHERE secretgroupid = ?', [item.id],
                                        (_, { rows }) => {
                                            rows._array.map((item, _) => {
                                                const secret = {
                                                    id: item.id,
                                                    username: item.username,
                                                    password: item.password,
                                                    title: item.title,
                                                    description: item.description,
                                                    url: item.url,
                                                    created_at: item.created_at,
                                                    updated_at: item.updated_at
                                                }
                                                secretgroup.secrets.push(secret);
                                            });
                                        }
                                    );
                                });
                                this.setState({ list: [...this.state.list] });
                            }
                        );
                        list.push(database);
                    }
                    );
                    this.setState({ list });

                },
                (_, error) => console.log(error))
        });
    }

    addDb = () => {
        this.db.transaction(tx => {
            tx.executeSql('INSERT INTO database (name) VALUES (?)', ['Database'],
                (txObj, resultSet) => {
                    console.log('Database added');
                    txObj.executeSql('SELECT * FROM database WHERE id = ?', [resultSet.insertId],
                        (_, { rows }) => {
                            rows._array.map((item, _) => {
                                const data = {
                                    id: resultSet.insertId,
                                    name: item.name,
                                    created_at: item.created_at,
                                    updated_at: item.updated_at,
                                    secretgroups: []
                                }
                                this.setState({ list: [...this.state.list, data] });
                                this.addSecretGroup('General', resultSet.insertId);
                                return;
                            });
                        }
                    );
                }
            ),
                (_, error) => console.log(error)
        })
    }

    addSecretGroup = (databaseid, name) => {
        this.db.transaction(tx => {
            tx.executeSql('INSERT INTO secretgroup (name, databaseid) VALUES (?, ?)', [name, databaseid],
                (txObj, resultSet) => {
                    console.log('Secret Group added');
                    txObj.executeSql('SELECT * FROM secretgroup WHERE id = ?', [resultSet.insertId],
                        (_, { rows }) => {
                            rows._array.map((item, _) => {
                                const data = {
                                    id: resultSet.insertId,
                                    name: item.name,
                                    created_at: item.created_at,
                                    updated_at: item.updated_at,
                                    secrets: []
                                }
                                const database = this.state.list.find(database => database.id === databaseid);
                                database.secretgroups.push(data);
                                this.setState({ list: [...this.state.list] });
                                return;
                            });
                        }
                    );

                }),
                (_, error) => console.log(error)
        })
    }

    addSecret = (databaseid, secretgroupid, username, password, title, description, url) => {
        this.db.transaction(tx => {
            tx.executeSql('INSERT INTO secret (username, password, title, description, url, secretgroupid) VALUES (?, ?, ?, ?, ?, ?)', [username, password, title, description, url, secretgroupid],
                (txObj, resultSet) => {
                    console.log('Secret added');
                    txObj.executeSql('SELECT * FROM secret WHERE id = ?', [resultSet.insertId],
                        (_, { rows }) => {
                            rows._array.map((item, _) => {
                                const data = {
                                    id: resultSet.insertId,
                                    username: item.username,
                                    password: item.password,
                                    title: item.title,
                                    description: item.description,
                                    url: item.url,
                                    created_at: item.created_at,
                                    updated_at: item.updated_at
                                }
                                const database = this.state.list.find(database => database.id === databaseid);
                                const secretgroup = database.secretgroups.find(secretgroup => secretgroup.id === secretgroupid);
                                secretgroup.secrets.push(data);
                                this.setState({ list: [...this.state.list] });
                                return;
                            });
                        }
                    );
                }),
                (_, error) => console.log(error)
        })
    }

    render() {
        const { expanded, list, open } = this.state;
        return (
            <View style={styles.container}>
                <ScrollView>
                    {list.map((database, i) => (
                        <ListItem.Accordion
                            key={i}
                            content={
                                <>
                                    <Icon type='ionicon' name="server" size={30} />
                                    <ListItem.Content>
                                        <ListItem.Title>{database.name}</ListItem.Title>
                                    </ListItem.Content>
                                </>
                            }
                            isExpanded={expanded}
                            onPress={() => {
                                this.setExpanded(!expanded);
                            }}
                        >
                            {database.secretgroups.map((secretgroup, j) => (
                                <ListItem key={j} bottomDivider
                                    onPress={() => this.navigation.navigate('Secrets', { databaseid: database.id, secretgroup, addSecret: this.addSecret })}
                                >
                                    <Icon type='ionicon' name="document-outline" />
                                    <ListItem.Content>
                                        <ListItem.Title>{secretgroup.name}</ListItem.Title>
                                        {/* <ListItem.Subtitle>********************</ListItem.Subtitle> */}
                                    </ListItem.Content>
                                    <ListItem.Chevron color="black" />
                                </ListItem>
                            ))}
                        </ListItem.Accordion>
                    ))}
                </ScrollView>
                <SpeedDial
                    isOpen={open}
                    color='#007AFF'
                    icon={{ type: 'ionicon', name: 'list', color: 'white' }}
                    openIcon={{ name: 'close', color: '#fff' }}
                    onOpen={() => this.setOpen(!open)}
                    onClose={() => this.setOpen(!open)}
                    onPress={() => this.setOpen(!open)}
                >
                    <SpeedDial.Action
                        color='white'
                        icon={{ type: 'ionicon', name: 'server' }}
                        title="Create sub database"
                        onPress={() => this.addDb()}
                    />
                    <SpeedDial.Action
                        color='#007AFF'
                        icon={{ type: 'ionicon', name: 'document', color: 'white' }}
                        title="Create secret group"
                        onPress={() => this.addSecretGroup()}
                    />
                    <SpeedDial.Action
                        color='orange'
                        icon={{ type: 'ionicon', name: 'key', color: 'white' }}
                        title="Create Secret"
                        onPress={() => this.addSecret()}
                    />
                    <SpeedDial.Action
                        color='#007AFF'
                        icon={{ name: 'add', color: 'white' }}
                        title="Fetch Data"
                        onPress={() => this.fetchData()}
                    />
                    <SpeedDial.Action
                        color='#007AFF'
                        icon={{ name: 'add', color: 'white' }}
                        title="Encrypt"
                        onPress={() => this.encrypt()}
                    />
                </SpeedDial>
            </View>
        )
    }
}
export default Home;

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: '#fff',
        justifyContent: 'center',
    }
});
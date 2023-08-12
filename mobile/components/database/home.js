import React from 'react';
import { StyleSheet, Text, View, ScrollView, FlatList } from 'react-native';
import { FAB, Icon, ListItem, Avatar } from 'react-native-elements';



export default function Home() {
    const list = [
        {
            name: 'Amy Farha',
            avatar_url: 'https://s3.amazonaws.com/uifaces/faces/twitter/ladylexy/128.jpg',
            subtitle: 'Vice President'
        },
        {
            name: 'Chris Jackson',
            avatar_url: 'https://s3.amazonaws.com/uifaces/faces/twitter/adhamdannaway/128.jpg',
            subtitle: 'Vice Chairman'
        },
    ]

    const [expanded, setExpanded] = React.useState(true);

    return (
        <View style={styles.container}>
            <ScrollView>
                <ListItem.Accordion
                    content={
                        <>
                            <Icon name="place" size={30} />
                            <ListItem.Content>
                                <ListItem.Title>List Accordion</ListItem.Title>
                            </ListItem.Content>
                        </>
                    }
                    isExpanded={expanded}
                    onPress={() => {
                        setExpanded(!expanded);
                    }}
                >
                    {list.map((item, i) => (
                        <ListItem key={i} bottomDivider>
                            <Icon type='ionicon' name="key-outline" />
                            <ListItem.Content>
                                <ListItem.Title>{item.name}</ListItem.Title>
                                <ListItem.Subtitle>{item.subtitle}</ListItem.Subtitle>
                            </ListItem.Content>
                            <ListItem.Chevron />
                        </ListItem>
                    ))}
                </ListItem.Accordion>
            </ScrollView>
            <FAB placement='right' color='#007AFF' icon={props => <Icon name="add" color="white" {...props} />} />
        </View>
    )
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: '#fff',
        //   alignItems: 'center',
        justifyContent: 'center',
    }
});
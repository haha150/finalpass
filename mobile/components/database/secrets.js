import React from 'react';
import { StyleSheet, Text, View } from 'react-native';

class Secrets extends React.Component {
    constructor(props) {
        super(props)
        this.state = {
            list: []
        }
    }

    render() {
        return (
            <View style={styles.container}>
                <Text>Secrets</Text>
            </View>
        )
    }
}
export default Secrets;

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: '#fff',
        //   alignItems: 'center',
        justifyContent: 'center',
    },
});

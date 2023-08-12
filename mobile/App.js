import { StatusBar } from 'expo-status-bar';
import { StyleSheet } from 'react-native';
import Home from './components/database/home';
import About from './components/misc/about';
import { NavigationContainer } from '@react-navigation/native';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import Ionicons from 'react-native-vector-icons/Ionicons';

const Tab = createBottomTabNavigator();

export default function App() {
  return (
    // <Navigator />
    // <View style={styles.container}>
    //   <Text>Open up ASDBD.js to start working on your app!</Text>
    //   <StatusBar style="dark" />
    // </View>
    <NavigationContainer>
      <Tab.Navigator
        screenOptions={({ route }) => ({
          tabBarIcon: ({ focused, color, size }) => {
            let iconName;

            if (route.name === 'Finalpass') {
              iconName = focused
                ? 'home'
                : 'home-outline';
            } else if (route.name === 'About') {
              iconName = focused ? 'information-circle' : 'information-circle-outline';
            }

            return <Ionicons name={iconName} size={size} color={color} />;
          },
          // tabBarActiveTintColor: 'tomato',
          // tabBarInactiveTintColor: 'gray',
        })}
      >
        <Tab.Screen name="Finalpass" component={Home} />
        <Tab.Screen name="About" component={About} />
      </Tab.Navigator>
      <StatusBar style="dark" />
    </NavigationContainer>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#fff',
    alignItems: 'center',
    justifyContent: 'center',
  },
});

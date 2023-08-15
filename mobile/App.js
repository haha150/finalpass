import { StatusBar } from 'expo-status-bar';
import { StyleSheet } from 'react-native';
import Home from './components/database/home';
import Secrets from './components/database/secrets';
import Account from './components/account/account';
import Settings from './components/misc/settings';
import About from './components/misc/about';
import { NavigationContainer } from '@react-navigation/native';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import Ionicons from 'react-native-vector-icons/Ionicons';

const Tab = createBottomTabNavigator();

const HomeStack = createNativeStackNavigator();

function HomeStackScreen() {
	return (
		<HomeStack.Navigator>
			<HomeStack.Screen name="Finalpass" component={Home} />
			<HomeStack.Screen name="Secrets" component={Secrets} />
		</HomeStack.Navigator>
	);
}

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
					headerShown: false,
					tabBarIcon: ({ focused, color, size }) => {
						let iconName;
						if (route.name === 'Finalpass') {
							iconName = focused ? 'lock-closed' : 'lock-closed-outline';
						} else if (route.name === 'Account') {
							iconName = focused ? 'person' : 'person-outline';
						} else if (route.name === 'Settings') {
							iconName = focused ? 'cog' : 'cog-outline';
						} else if (route.name === 'About') {
							iconName = focused ? 'information-circle' : 'information-circle-outline';
						}
						return <Ionicons name={iconName} size={size} color={color} />;
					},
					// tabBarActiveTintColor: 'tomato',
					// tabBarInactiveTintColor: 'gray',
				})}
			>
				<Tab.Screen name="Finalpass" component={HomeStackScreen} />
				<Tab.Screen name="Account" component={Account} />
				<Tab.Screen name="Settings" component={Settings} />
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

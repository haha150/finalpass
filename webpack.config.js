// webpack.config.js
module.exports = [
    {
      mode: 'development',
      entry: './src/main.js',
      target: 'electron-main',
      output: {
        path: __dirname + '/dist',
        filename: 'electron.js'
      }
    }
  ];
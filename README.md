# NebliDex - An atomic swap powered decentralized exchange
NebliDex is a full service decentralized exchange that users can use to trade cryptocurrencies such as Bitcoin, Litecoin, Ethereum, Neblio, Monacoin, Groestlcoin, Bitcoin Cash and Neblio based assets (NTP1 tokens) without using a centralized service or match maker. Now available on mobile phones via its Android port. Bitcoin in NebliDex represents actual Bitcoin. Ethereum in NebliDex represents actual Ethereum. There are no gateways to convert Bitcoin to any other representative tokens. The trades are performed using atomic swap functionality as defined by the Decred specification with some modifications and the Ethereum atomic swap contract can be found here: https://etherscan.io/address/0xcfd9c086635cee0357729da68810a747b6bc674a

NebliDex also supports ERC20 stablecoins USDC & DAI. Its ERC20 atomic swap contract can be found here:
https://etherscan.io/address/0x1784e5AeC9AD99445663DBCA9462a618BfE545Ac

The matchmaking is performed by Critical Nodes on the network which are volunteers that have at least 39,000 NDEX tokens. Matchmakers/Validators get rewarded by receiving NDEX tokens for their service. Anyone can become a validator as long as they meet the requirements specified in readme_first document. The mobile application does not support critical node functionality. Users must download the desktop app.

## Bug Reports
If a bug or vulnerability is found, please export your debug.log via the settings then report it immediately via our bug report form: https://www.neblidex.xyz/bugreport/

## Getting Started
First read readme_first.html before creating any trade. NebliDex is very intuitive.

## Building NebliDex
NebliDex is built in C# using managed code from the .NET Library on Windows and Mono Framework on Mac, Linux and Android.
NebliDex uses Newtonsoft.JSON library (JSON.NET) and SQLite Library Version 3. Download them from NuGet sources in the editor.
### Android
* Download Visual Studio
* Install Xamarin cross platform toolkit
* Open Solution
* Install dependencies mentioned above
* Build and Run in Android emulator or on device

## Release Notes
### https://www.neblidex.xyz/downloads/#release_notes

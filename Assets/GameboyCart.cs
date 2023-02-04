using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEngine;
using KModkit;

public class GameboyCart : MonoBehaviour {

	//-----------------------------------------------------//
	public KMBombInfo Bomb;
	public KMAudio Audio;
	public KMBombModule Module;
	//public KMGameInfo StateCheck;

	public KMSelectable[] boardPins;
	public Renderer stickerMesh;
	public Material[] stickerTypes;
	public TextMesh stickerLabel;

	public Renderer[] addressLights;
	public Renderer[] dataLights;
	public Renderer powerLight;
	public Material[] lightMats;

	public bool debugMode;
	public int debugSticker;
	public string debugLabel;
	public int debugRegion;

	//-----------------------------------------------------//
	private int sticker = 0;
	//private int regionCode = 15;

	private bool powered = false;
	private bool reset = false;
	private bool write = false;
	private bool read = false;
	private int address = 0;

	private bool[,] addressStates = new bool[,] {
		{false, false, false, false}, // A0
		{false, false, false, false}, // A1
		{false, false, false, false}  // A2
	};
	private bool[,] solutionStates = new bool[,] {
		{false, true, true, true}, // A0
		{false, false, false, true}, // A1
		{false, true, true, false}  // A2
	};

	private int[] regionTagVal = new int[] {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0};

	private bool Solved = false;
	//-----------------------------------------------------//
	private string[] regionHex = new string[] { "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V" };
	private string[] hexChars = new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "A", "B", "C", "D", "E", "F" };
	private int[,] stickerShuffles = new int[,] { {0, 1, 2}, {2, 1, 0}, {1, 0, 2}, {2, 0, 1}, {0, 2, 1}, {1, 2, 0} };
	private string[] stickerNames = new string[] { "Keep Talking", "Dr. Eggman's Empire Sim", "Blan's Bananas 2", "Hentai / Censored", "Bamboo Defender", "Pocket Dwarf" };
	//private int[] dmgMultiply = new int[] {1, 2, 4, 8};
	/*private string[] regions = new string[] {
		"ASI", // 00 Asia (Taiwan, ...)
		"AUS", // 01 Australia
		"CAN", // 02 Canada
		"CHN", // 03 China
		"ESP", // 04 Spain (España)
		"EUR", // 05 Europe and Australia
		"FAH", // 06 France And Holland
		"FRA", // 07 France and sometimes Belgium
		"FRG", // 08 Federal ???? Germany (Austria, Switzerland, Germany)
		"GPS", // 09 Game Package Software (distribution for other little countries with no code)
		"HOL", // 10 Netherlands and sometimes Belgium
		"ITA", // 11 Italia
		"JAP", // 12 Japan
		"NOE", // 13 Nintendo of Europe (Germany)
		"SCN", // 14 Scandinavia (Sweden, Norway, Denmark, Finland)
		"UKV", // 15 United Kingdom
		"USA"  // 16 USA & sometimes Mexico
	};*/

	private int[] addressPins = new int[] {3, 4, 5};
	private int[] dataPins = new int[] {7, 8, 9, 10};

	//-----------------------------------------------------//
	static int moduleIdCounter = 1;
	int moduleId;
	//-----------------------------------------------------//

	private void Awake () {
		moduleId = moduleIdCounter++;

		foreach (KMSelectable NAME in boardPins) {
			KMSelectable pressedObject = NAME;
			NAME.OnInteract += delegate () { Select(pressedObject); return false; };
		}
	}

	void Start () {
		InitSolution();
		Debug.Log("Hiding Solution");
	}
	
	void InitSolution () {
		string gameTag = "A";
		string regionTag = "";
		sticker = UnityEngine.Random.Range(0, 6);
		if (debugMode) { sticker = debugSticker; } //gameTag = debugLabel;

		Debug.LogFormat("[Gameboy Cartridge #{0}] Sticker is {1}. Address order is {2}-{3}-{4}" , moduleId, stickerNames[sticker], stickerShuffles[sticker, 0]+1, stickerShuffles[sticker, 1]+1, stickerShuffles[sticker, 2]+1);

		for (int i = 0; i < 12; i++) {
			regionTagVal[i] = UnityEngine.Random.Range(0, 2);
			if (i % 4 == 3) { regionTag += regionHex[(regionTagVal[i-3] * 8) + (regionTagVal[i-2] * 4) + (regionTagVal[i-1] * 2) + (regionTagVal[i])]; }
		}
		Debug.LogFormat("[Gameboy Cartridge #{0}] Region string is {1}. Modifier codes are [{2}{3}{4}{5}] [{6}{7}{8}{9}] [{10}{11}{12}{13}]" , moduleId, regionTag, regionTagVal[0], regionTagVal[1], regionTagVal[2], regionTagVal[3], regionTagVal[4], regionTagVal[5], regionTagVal[6], regionTagVal[7], regionTagVal[8], regionTagVal[9], regionTagVal[10], regionTagVal[11]);

		//if (debugMode) { }

		int[] gameTagVal = new int[] {0, 0, 0, 0};
		for (int a = 0; a < 3; a++) {
			int row = stickerShuffles[sticker, a];
			//Debug.Log("Row " + row);
			for (int d = 0; d < 4; d++) {
				if (!debugMode) {
					if (UnityEngine.Random.Range(0, 2) == 1) { solutionStates[row, d] = true; gameTagVal[d] = 1; } else { solutionStates[row, d] = false; gameTagVal[d] = 0; }
				} else if (solutionStates[row, d]) { gameTagVal[d] = 1; } else { gameTagVal[d] = 0; }
				//Debug.Log(regionTagVal[a * 4 + d] + " // " + solutionStates[a, d]);
				//Debug.Log(regionTagVal[row * 4 + d]);
				//Debug.Log(solutionStates[row, d]);
				if (regionTagVal[row * 4 + d] == 1) { solutionStates[row, d] = !solutionStates[row, d]; }
			}
			gameTag += hexChars[(gameTagVal[0] * 8) + (gameTagVal[1] * 4) + (gameTagVal[2] * 2) + (gameTagVal[3])];
			Debug.LogFormat("[Gameboy Cartridge #{0}] Requesting A{1} -- [D0/{2}] [D1/{3}] [D2/{4}] [D3/{5}]", moduleId, row, solutionStates[row, 0], solutionStates[row, 1], solutionStates[row, 2], solutionStates[row, 3]);
		}
		stickerMesh.material = stickerTypes[sticker];
		stickerLabel.text = "DMG - " + gameTag + " - " + regionTag; //regions[regionCode]
		return;
	}

	void Select (KMSelectable Pin) {
		Audio.PlaySoundAtTransform("click60ALT", transform);
		int pinNum = Array.IndexOf(boardPins, Pin);
		//Debug.LogFormat("[Gameboy Cartridge #{0}] Pin {1} Pressed", moduleId, pinNum + 1);
		if (pinNum == 0 && !powered) { powered = true; powerLight.material = lightMats[1]; Debug.LogFormat("[Gameboy Cartridge #{0}] Powered ON", moduleId); } else if (!powered) { return; }
		if (pinNum == 1) { IdleState(); reset = true; Debug.LogFormat("[Gameboy Cartridge #{0}] Switched to RESET", moduleId); }
		if (pinNum == 2) { IdleState(); write = true; Debug.LogFormat("[Gameboy Cartridge #{0}] Switched to WRITE", moduleId); }
		if (pinNum == 6) { IdleState(); CheckSolve(); }
		if (pinNum == 11) { IdleState(); read = true; Debug.LogFormat("[Gameboy Cartridge #{0}] Switched to READ", moduleId); }
		if (pinNum == 12 && powered) { PowerOff(); }

		if (addressPins.Contains(pinNum)) {
			address = pinNum - 3;
			if (reset) {
				Debug.LogFormat("[Gameboy Cartridge #{0}] Resetting Address {1}", moduleId, address);
				for (int d = 0; d < 4; d++) { addressStates[address, d] = false; }
				return;
			} else if (read) {
				//Debug.LogFormat("[Gameboy Cartridge #{0}] Selected Address {1}", moduleId, address);
				for (int a = 0; a < 3; a++) { if (a == address) { addressLights[a].material = lightMats[1]; } else { addressLights[a].material = lightMats[0]; } }
				for (int d = 0; d < 4; d++) {
					if (addressStates[address, d]) { dataLights[d].material = lightMats[3]; } else { dataLights[d].material = lightMats[2]; }
					//Debug.Log(addressStates[address, d]);
				}
			}
			Debug.LogFormat("[Gameboy Cartridge #{0}] Selected Address {1}", moduleId, address);
		}

		if (dataPins.Contains(pinNum)) {
			int data = pinNum - 7;
			if (write) {
				addressStates[address, data] = !addressStates[address, data];
				Debug.LogFormat("[Gameboy Cartridge #{0}] Set A{1} D{2} to {3}", moduleId, address, data, addressStates[address, data]);
			}
			
		}
		
		//Module.HandlePass();
	}

	void IdleState () {
		reset = false;
		read = false;
		write = false;
	}

	void PowerOff () {
		Debug.LogFormat("[Gameboy Cartridge #{0}] Powered OFF", moduleId);
		addressStates = new bool[,] {
			{false, false, false, false}, // A0
			{false, false, false, false}, // A1
			{false, false, false, false}  // A2
		};
		foreach (Renderer LIGHT in addressLights) { LIGHT.material = lightMats[0]; }
		foreach (Renderer LIGHT in dataLights) { LIGHT.material = lightMats[2]; }
		powerLight.material = lightMats[0];
		IdleState();
		powered = false;
	}

	void CheckSolve () {
		if (Solved) { return; }
		for (int a = 0; a < 3; a++) {
			for (int d = 0; d < 4; d++) {
				if (addressStates[a, d] != solutionStates[a, d]) {
					Debug.LogFormat("[Gameboy Cartridge #{0}] A{1} D{2} was incorrect. Read {3}. Should be {4}", moduleId, a, d, addressStates[a, d], solutionStates[a, d]);
					Module.HandleStrike();
					return;
				}
			}
		}
		Debug.LogFormat("[Gameboy Cartridge #{0}] Data accepted", moduleId);
		Solved = true;
		Module.HandlePass();
	}
	
		// Twitch Plays

#pragma warning disable 414
	private readonly string TwitchHelpMessage = @"!{0} Pin 1-F 1-F... // powers a pin. You can sttring as many pins together as you want";
#pragma warning restore 414

	int checkValidPin(string n) {
		string[] valids = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "A", "B", "C", "D" };
		if (!valids.Contains(n)) { return 13; }
		return Array.IndexOf(valids, n);;
	}

	IEnumerator ProcessTwitchCommand (string command) {
		yield return null;

		string[] split = command.ToUpperInvariant().Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);

		if (split[0].EqualsIgnoreCase("PIN")) {
			if (split.Length < 2) {
				yield return "sendtochaterror Please specify which pin to power!";
				yield break;
			}
			foreach (string PIN in split) {
				if (PIN.EqualsIgnoreCase("PIN")) { continue; }
				if (checkValidPin(PIN) == 13) {
					yield return "sendtochaterror " + PIN + " is not a valid pin!";
					yield break;
				}
			}

			for (int i = 1; i < split.Length; i++){
				int pinNum = checkValidPin(split[i]);
				boardPins[pinNum].OnInteract();
				yield return new WaitForSeconds(0.1f);
			}
			yield break;
		}
	}

	IEnumerator TwitchHandleForcedSolve() //Autosolver
	{
		yield return null;

		if (powered) {
			boardPins[12].OnInteract();
			yield return new WaitForSeconds(0.1f);
		}
		boardPins[0].OnInteract();
		yield return new WaitForSeconds(0.1f);
		for (int a = 0; a < 3; a++) {
			boardPins[2].OnInteract();
			yield return new WaitForSeconds(0.1f);
			boardPins[a + 3].OnInteract();
			yield return new WaitForSeconds(0.1f);
			for (int d = 0; d < 4; d++) {
				if (solutionStates[a, d]) { boardPins[d + 7].OnInteract(); }
				yield return new WaitForSeconds(0.1f);
			}
			boardPins[11].OnInteract();
			yield return new WaitForSeconds(0.1f);
			boardPins[a + 3].OnInteract();
			//Debug.Log("This should only show once");
			yield return new WaitForSeconds(0.5f);
		}
		boardPins[6].OnInteract();
	}
}

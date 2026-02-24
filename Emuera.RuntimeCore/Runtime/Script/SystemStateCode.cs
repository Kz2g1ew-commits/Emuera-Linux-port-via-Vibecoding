namespace MinorShift.Emuera.Runtime.Script;

internal enum SystemStateCode
{
	__CAN_SAVE__ = 0x10000,//セーブロード画面を呼び出し可能か？
	__CAN_BEGIN__ = 0x20000,//BEGIN命令を呼び出し可能か？
	Title_Begin = 0,//初期状態
	Openning = 1,//最初の入力待ち
	Train_Begin = 0x10,//BEGIN TRAINから。
	Train_CallEventTrain = 0x11,//@EVENTTRAINの呼び出し中。スキップ可能
	Train_CallShowStatus = 0x12,//@SHOW_STATUSの呼び出し中
	Train_CallComAbleXX = 0x13,//@COM_ABLExxの呼び出し中。スキップの場合、RETURN 1とする。
	Train_CallShowUserCom = 0x14,//@SHOW_USERCOMの呼び出し中
	Train_WaitInput = 0x15,//入力待ち状態。選択が実行可能ならEVENTCOMからCOMxx、そうでなければ@USERCOMにRESULTを渡す
	Train_CallEventCom = 0x16 | __CAN_BEGIN__,//@EVENTCOMの呼び出し中

	Train_CallComXX = 0x17 | __CAN_BEGIN__,//@COMxxの呼び出し中
	Train_CallSourceCheck = 0x18 | __CAN_BEGIN__,//@SOURCE_CHECKの呼び出し中
	Train_CallEventComEnd = 0x19 | __CAN_BEGIN__,//@EVENTCOMENDの呼び出し中。スキップ可能。Train_CallEventTrainへ帰る。@USERCOMの呼び出し中もここ

	Train_DoTrain = 0x1A,

	AfterTrain_Begin = 0x20 | __CAN_BEGIN__,//BEGIN AFTERTRAINから。@EVENTENDを呼び出してNormalへ。

	Ablup_Begin = 0x30,//BEGIN ABLUPから。
	Ablup_CallShowJuel = 0x31,//@SHOW_JUEL
	Ablup_CallShowAblupSelect = 0x32,//@SHOW_ABLUP_SELECT
	Ablup_WaitInput = 0x33,//
	Ablup_CallAblupXX = 0x34 | __CAN_BEGIN__,//@ABLUPxxがない場合は、@USERABLUPにRESULTを渡す。Ablup_CallShowJuelへ戻る。

	Turnend_Begin = 0x40 | __CAN_BEGIN__,//BEGIN TURNENDから。@EVENTTURNENDを呼び出してNormalへ。

	Shop_Begin = 0x50 | __CAN_SAVE__,//BEGIN SHOPから
	Shop_CallEventShop = 0x51 | __CAN_BEGIN__ | __CAN_SAVE__,//@EVENTSHOPの呼び出し中。スキップ可能
	Shop_CallShowShop = 0x52 | __CAN_SAVE__,//@SHOW_SHOPの呼び出し中
	Shop_WaitInput = 0x53 | __CAN_SAVE__,//入力待ち状態。アイテムが存在するならEVENTBUYにBOUGHT、そうでなければ@USERSHOPにRESULTを渡す
	Shop_CallEventBuy = 0x54 | __CAN_BEGIN__ | __CAN_SAVE__,//@USERSHOPまた@EVENTBUYはの呼び出し中

	SaveGame_Begin = 0x100,//SAVEGAMEから
	SaveGame_WaitInput = 0x101,//入力待ち
	SaveGame_WaitInputOverwrite = 0x102,//上書きの許可待ち
	SaveGame_CallSaveInfo = 0x103,//@SAVEINFO呼び出し中。20回。
	LoadGame_Begin = 0x110,//LOADGAMEから
	LoadGame_WaitInput = 0x111,//入力待ち
	LoadGameOpenning_Begin = 0x120,//最初に[1]を選択したとき。
	LoadGameOpenning_WaitInput = 0x121,//入力待ち


	//AutoSave_Begin = 0x200,
	AutoSave_CallSaveInfo = 0x201,
	AutoSave_CallUniqueAutosave = 0x202,
	AutoSave_Skipped = 0x203,

	LoadData_DataLoaded = 0x210,//データロード直後
	LoadData_CallSystemLoad = 0x211 | __CAN_BEGIN__,//データロード直後
	LoadData_CallEventLoad = 0x212 | __CAN_BEGIN__,//@EVENTLOADの呼び出し中。スキップ可能

	Openning_TitleLoadgame = 0x220,

	System_Reloaderb = 0x230,
	First_Begin = 0x240,

	Normal = 0xFFFF | __CAN_BEGIN__ | __CAN_SAVE__,//特に何でもないとき。ScriptEndに達したらエラー
}

extends PanelContainer

func _ready():
	$"%Device".text += "\ndrawbook: " + Drawbook.find_me() + "\n"
	$task_test.submit(IPCTaskTest.new(), self, "done")

func done(result):
	var fail = null
	if result[1] == null:
		$"%Device".text += "Error executing drawbook: " + str(result[0])
		fail = "EXECUTION ERROR"
	else:
		var info: String = result[1]
		$"%Device".text += "Process output:\n" + result[1]
		if info.count("INVOKE-OK") > 0:
			pass
		else:
			fail = "NO INVOKE-OK SIGNAL"
	if fail != null:
		$"%Device".text += "\n\n *** PROBLEM: " + fail + " - FIX IT AND RESTART ***"

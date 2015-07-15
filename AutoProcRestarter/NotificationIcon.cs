/**
 * AutoProcRestarter - Version 1.0
 * Copyright (c) 2014, Petros Kyladitis <www.multipetros.gr>
 * 
 * This program is free software distributed under the GNU GPL 3,
 * for license details see at 'license.txt' file, distributed with
 * this project, or see at <http://www.gnu.org/licenses/>.
 **/
 
using System ;
using System.Diagnostics ;
using System.Drawing ;
using System.Threading ;
using System.Windows.Forms ;
using System.IO ;
using Multipetros ;

namespace AutoProcRestarter{
	public sealed class NotificationIcon{
		private NotifyIcon notifyIcon;
		private ContextMenu notificationMenu;		
		
		private const string INI_NAME = "AutoProcRestarter.ini" ;
		private const string INI_PARAM_EXE = "EXE_NAME" ;
		private const string INI_PARAM_TIME = "RESET_TIME" ;
		
		private string exeName = string.Empty ;
		private int resetTime = 0 ;
		private System.Windows.Forms.Timer resetTimer = new System.Windows.Forms.Timer() ;
		private Process proc ;
		private Props ini = new Props(INI_NAME, true) ;
		private OpenFileDialog exeSelector = new OpenFileDialog() ;
		private bool stopped = false ;        //indicate if auto restarting is stopped
		private bool procUserExited = false ; //indicate if user closed the restarting proc
		
		// Initialize icon and menu
		public NotificationIcon(){
			notifyIcon = new NotifyIcon();
			notificationMenu = new ContextMenu(InitializeMenu());
			
			notifyIcon.DoubleClick += IconDoubleClick;
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NotificationIcon));
			notifyIcon.Icon = (Icon)resources.GetObject("$this.Icon");
			notifyIcon.ContextMenu = notificationMenu;
		}
		
		private MenuItem[] InitializeMenu(){
			MenuItem[] menu = new MenuItem[] {
				new MenuItem("S&top Restarting", MenuStartStopRestartClick),
				new MenuItem("-"),
				new MenuItem("Executable &Path", MenuExeClick),
				new MenuItem("T&ime to Reset", MenuTimeClick),
				new MenuItem("-"),
				new MenuItem("&About", MenuAboutClick),
				new MenuItem("E&xit", MenuExitClick)
			};
			return menu;
		}
		
		// Main - Program entry point
		[STAThread]
		public static void Main(string[] args){
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			
			bool isFirstInstance;
			// Please use a unique name for the mutex to prevent conflicts with other programs
			using (Mutex mtx = new Mutex(true, "AutoProcRestarter", out isFirstInstance)) {
				if (isFirstInstance) {
					NotificationIcon notificationIcon = new NotificationIcon();
					notificationIcon.notifyIcon.Visible = true;
					notificationIcon.StartAutoProc() ;
					Application.Run();
					notificationIcon.notifyIcon.Dispose();
				} else {
					MessageBox.Show("AutoProcRestarter is already running!\nYou can't open a second instance.","AutoProcRestarter is running", MessageBoxButtons.OK, MessageBoxIcon.Exclamation) ;
				}
			} // releases the Mutex
		}
		
		/* *** EVENT HANDLERS *** */
		
		private void MenuAboutClick(object sender, EventArgs e){
			resetTimer.Stop() ;
			MessageBox.Show("AutoProcRestarter - Ver 1.0\nA system tray utility for running and restarting programs, after the selected time\n\n(c) 2014, Petros Kyladitis\n<http://www.multipetros.gr>\n\nThis program is free software distributed under the GNU GPL 3, for license details see at 'license.txt' file, distributed with this program, or see at <http://www.gnu.org/licenses/>.", "About AutoProcRestarter", MessageBoxButtons.OK, MessageBoxIcon.Information) ;
			if(!stopped && !procUserExited)
				resetTimer.Start() ;
		}
		
		//if selected program is running, aks user to terminate it with the AutoProcRestarter
		private void MenuExitClick(object sender, EventArgs e){
			resetTimer.Stop() ;
			
			if(proc == null){
				Environment.Exit(0) ;
			}
			
			if(!procUserExited){
				DialogResult result = MessageBox.Show("Terminating also " + proc.MainWindowTitle + "?", "Closing AutoProcRestarter", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) ;
				if(result == DialogResult.Cancel){
					if(!stopped){
						resetTimer.Start() ;
					}
					return ;
				}
				else if(result == DialogResult.Yes){
					proc.CloseMainWindow() ;
					proc.WaitForExit() ;
				}
			}
			Application.Exit();
		}
		
		private void IconDoubleClick(object sender, EventArgs e){
			resetTimer.Stop() ;
			MessageBox.Show("Running: " + exeName + "\nRestart every: " + resetTime.ToString() + " secs", "AutoProcRestarter Running Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
			if(!stopped && !procUserExited)
				resetTimer.Start() ;
		}
		
		private void MenuExeClick(object sender, EventArgs e){
			SelectExe(false) ;
		}
		
		private void MenuTimeClick(object sender, EventArgs e){
			SetResetTime(false) ;
		}
		
		private void MenuStartStopRestartClick(object sender, EventArgs e){
			if(procUserExited){
				procUserExited = false ;
				StartAutoProc() ;
				notifyIcon.ContextMenu.MenuItems[0].Text = "S&top Restarting" ;
			}else{
				if(stopped){
					stopped = false ;
					resetTimer.Start() ;
					notifyIcon.ContextMenu.MenuItems[0].Text = "S&top Restarting" ;
				}else{
					stopped = true ;
					resetTimer.Stop() ;
					notifyIcon.ContextMenu.MenuItems[0].Text = "S&tart Restarting" ;
				}
			}
		}
		
		/* *** END - EVENT HANDLERS - END *** */
		
		//provide an input box, to the user, to set the restarting time
		private void SetResetTime(bool noCancel){
			resetTimer.Stop() ;
			bool canceled = false ;
			
			do{
				//use interaction class to call VB InputBox Function
				string timeInput = Microsoft.VisualBasic.Interaction.InputBox("Time in seconds, between each proc resseting", "Reset every...", resetTime.ToString(), 300, 300) ;
				int currentResetTime = resetTime ;
				int.TryParse(timeInput, out resetTime) ;
				if(resetTime < 3){
					resetTime = currentResetTime ;
					if(noCancel){
						canceled = true ;
					}
				}
			}while(canceled) ;
			
			ini.SetProperty(INI_PARAM_TIME, resetTime.ToString()) ;
			if(!stopped && !procUserExited)
				resetTimer.Start() ;
		}
		
		//provide a common dialog box to the user to select the file to run
		private void SelectExe(bool noCancel){
			resetTimer.Stop() ;
			exeSelector.CheckFileExists = true ;
			bool canceled = false ;
			
			do{
				if(exeSelector.ShowDialog() == DialogResult.OK){
					exeName = exeSelector.FileName ;
					ini.SetProperty(INI_PARAM_EXE, exeName) ;				
				}else{
					if(noCancel){
						canceled = true ;
					}
				}
			}while(canceled) ;
			
			if(!stopped && !procUserExited)
				resetTimer.Start() ;
		}
		
		//load user settings, execute the selected process and start to restarting it
		private void StartAutoProc(){
			//until load right values, prompt user to correct them
			do{
				exeName = ini.GetProperty(INI_PARAM_EXE, true) ;
				int.TryParse(ini.GetProperty(INI_PARAM_TIME, true), out resetTime) ;
				
				//if none program is selected or it doesn't exist, prompt user to select it
				if(exeName == string.Empty){
					MessageBox.Show("None executable selected to run and restart. Configure it now.", "AutoProcRestarter Configuration", MessageBoxButtons.OK, MessageBoxIcon.Exclamation) ;
					SelectExe(true) ;
				}
				else if(!File.Exists(exeName)){
					MessageBox.Show("Selected executable to run and restart is not found.", "AutoProcRestarter Configuration", MessageBoxButtons.OK, MessageBoxIcon.Error) ;
					SelectExe(true) ;
				}
				
				//if no time to restart selected or is invalid, prompt user to redefine it
				if(resetTime < 3){
					MessageBox.Show("Specify a reset time can't be over the 3 secs.", "AutoProcRestarter Configuration", MessageBoxButtons.OK, MessageBoxIcon.Exclamation) ;
					SetResetTime(true) ;
				}
			}while(exeName == string.Empty || !File.Exists(exeName) || resetTime < 3) ;
			
			procUserExited = false ;
			proc = Process.Start(exeName) ;
			resetTimer.Interval = resetTime * 1000 ;
			resetTimer.Tick += new EventHandler(resetTimer_Tick);
			resetTimer.Start() ;
		}

		//close and restart the specified process and the counter
		//if process exited by the user, prompt user and don't restart the counter
		private void resetTimer_Tick(object sender, EventArgs e){
			if(!proc.HasExited){
				proc.CloseMainWindow() ;
				proc.WaitForExit() ;
				proc = Process.Start(exeName) ; //start the process again (if user change the exe, this see the new user selection)
				resetTimer.Interval = resetTime * 1000 ; //redefine the reset time (for user changes)
				resetTimer.Start() ; //restart the timer
			}else{
				resetTimer.Stop() ;
				procUserExited = true ; //mark that user close the program
				notifyIcon.ContextMenu.MenuItems[0].Text = "S&tart Restarting" ; //change menu item string
				MessageBox.Show("Process stopped maybe by the user.\nAutoProcRestarter stop the automatic restarts.\nTo enable them again, right click on the system\ntray icon and select \"Start Restarting\"", "AutoProcRestarter Stopped", MessageBoxButtons.OK, MessageBoxIcon.Exclamation) ;
			}
		}
	}
}
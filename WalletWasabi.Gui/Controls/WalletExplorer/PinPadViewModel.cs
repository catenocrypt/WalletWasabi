using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Text;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	internal class PinPadViewModel : WalletActionViewModel
	{
		private CompositeDisposable Disposables { get; set; }

		/*
		 * 7 8 9
		 * 4 5 6
		 * 1 2 3
		 */

		public PinPadViewModel(WalletViewModel walletViewModel) : base("Pin Pad", walletViewModel)
		{
		}

		public override void OnOpen()
		{
			base.OnOpen();

			if (Disposables != null)
			{
				throw new Exception("Pin Pad Tab was opened before it was closed.");
			}

			Disposables = new CompositeDisposable();
		}

		public override bool OnClose()
		{
			Disposables.Dispose();
			Disposables = null;

			return base.OnClose();
		}

		private void OnException(Exception ex)
		{
			SetWarningMessage(ex.ToTypeMessageString());
		}
	}
}

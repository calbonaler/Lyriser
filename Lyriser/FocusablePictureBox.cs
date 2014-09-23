using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Lyriser
{
	public class FocusablePictureBox : PictureBox
	{
		protected override void OnClick(EventArgs e)
		{
			base.OnClick(e);
			Select();
		}

		protected override bool IsInputKey(Keys keyData)
		{
			if ((keyData & Keys.Left) == Keys.Left || (keyData & Keys.Right) == Keys.Right || (keyData & Keys.Up) == Keys.Up || (keyData & Keys.Down) == Keys.Down)
				return true;
			return base.IsInputKey(keyData);
		}
	}
}

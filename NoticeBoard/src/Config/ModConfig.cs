
namespace NoticeBoard.Configs
{
    public class ModConfig
    {
        private double _divisionForPapersOnBoard = 1;

        public double DivisionForPapersOnBoard
        {
            get => _divisionForPapersOnBoard;
            set => _divisionForPapersOnBoard = value > 0 ? value : 1;
        }
    }
}

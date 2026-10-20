// 経験値オーブ
public class ExpOrb : Pickup
{
    private float amount;

    public void Init(float value)
    {
        amount = value;
    }

    protected override void OnCollected(PlayerStats player)
    {
        player.AddExp(amount);
    }
}

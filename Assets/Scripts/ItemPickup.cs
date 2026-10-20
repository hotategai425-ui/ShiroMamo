// 武器・防具のドロップ品
public class ItemPickup : Pickup
{
    private ItemData item;

    public void Init(ItemData data)
    {
        item = data;
    }

    protected override void OnCollected(PlayerStats player)
    {
        player.Equipment.ReceiveItem(item);
    }
}

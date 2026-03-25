import java.util.Random;
/**
 * Generates an RNG test vector.
 */
class RandTest {
	public static void main(String[] s) {
		Random r = new Random();
		r.setSeed(0);
		for (int i = 0; i < 4; i++) {
			int a = r.nextInt();
			System.out.println(a & 0x7FFFFFFF);
		}
	}
}

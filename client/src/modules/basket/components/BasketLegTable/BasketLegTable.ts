import { defineComponent, type PropType } from 'vue'
import { useI18n } from 'vue-i18n'

const markets: server.MarketKind[] = ['Fx', 'Metal', 'Index']
const directions: server.LegDirection[] = ['Long', 'Short']
const timeFrames: server.TimeFrame[] = ['M5', 'M15', 'M30', 'H1']

/**
 * Editable composition grid. It never mutates the legs it receives: every change emits a new array
 * through `update:legs`, so the owning view stays the single source of truth for the draft.
 */
export default defineComponent({
  name: 'BasketLegTable',
  props: {
    legs: {
      type: Array as PropType<server.BasketCompositionLeg[]>,
      required: true,
    },
    disabled: {
      type: Boolean,
      default: false,
    },
  },
  emits: {
    'update:legs': (_legs: server.BasketCompositionLeg[]) => true,
  },
  setup(props, { emit }) {
    const { t } = useI18n()

    function patchLeg(index: number, patch: Partial<server.BasketCompositionLeg>): void {
      const next = props.legs.map((leg, position) =>
        position === index ? { ...leg, ...patch } : leg,
      )

      emit('update:legs', next)
    }

    function removeLeg(index: number): void {
      emit(
        'update:legs',
        props.legs.filter((_leg, position) => position !== index),
      )
    }

    return { t, markets, directions, timeFrames, patchLeg, removeLeg }
  },
})

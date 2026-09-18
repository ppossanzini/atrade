import { computed, defineComponent } from 'vue'
import { useI18n } from 'vue-i18n'

/**
 * Renders one labelled value. The label is always an i18n key resolved inside the component;
 * the value is either an i18n key (fixed vocabulary such as enum names) or a runtime value
 * such as a timestamp.
 */
export default defineComponent({
  name: 'MetricRow',
  props: {
    labelKey: {
      type: String,
      required: true,
    },
    valueKey: {
      type: String,
      default: '',
    },
    value: {
      type: String,
      default: '',
    },
    muted: {
      type: Boolean,
      default: false,
    },
  },
  setup(props) {
    const { t } = useI18n()

    const displayValue = computed(() => (props.valueKey ? t(props.valueKey) : props.value))

    return { t, displayValue }
  },
})
